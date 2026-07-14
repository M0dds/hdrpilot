using System.Runtime.InteropServices;

namespace HdrPilot.Core;

/// <summary>
/// Best-Effort-Erkennung, ob NVIDIA RTX HDR (intern "TrueHDR") für ein Spiel
/// oder global aktiviert ist. RTX HDR und Windows Auto-HDR wenden beide
/// HDR-Tone-Mapping an und geraten in Konflikt - die UI warnt daher, wenn
/// beim Aktivieren von Auto-HDR RTX HDR erkannt wird.
///
/// Gelesen wird das NVIDIA-Treiberprofil (DRS) über nvapi64.dll:
///  - 0x00432F84 "TrueHDR flags": Bit 1 = aktiv (NvTrueHDR-Ära).
///  - 0x00DD48FB "Enable TrueHDR": 1 = aktiv (R550/NVIDIA-App-Ära).
///
/// ACHTUNG, verifizierte Einschränkung (Treiber 610.74, Juli 2026): Die
/// aktuelle NVIDIA App speichert den Per-Spiel-RTX-HDR-Schalter NICHT mehr
/// in DRS, sondern in ihrer eigenen Datenbank (CEF IndexedDB) - dort gesetzte
/// Zustände sind für diese Erkennung unsichtbar, und umgekehrt ignoriert der
/// Treiber in DRS geschriebene TrueHDR-Settings. Deshalb ist dies nur eine
/// Best-Effort-Warnung (ältere Treiber/Tools wie Profile Inspector), kein
/// Schalter: HDR Pilot kann RTX HDR auf aktuellen Treibern nicht setzen.
///
/// Alle Aufrufe sind defensiv: ohne NVIDIA-Treiber oder bei unerwarteten
/// Strukturen liefert die Erkennung null ("unbekannt").
/// </summary>
internal static class RtxHdrDetector
{
    private const uint TrueHdrFlagsSettingId = 0x00432F84;
    private const uint LegacyTrueHdrSettingId = 0x00DD48FB;
    private const uint FlagsEnableBit = 0x2;

    // nvapi_QueryInterface-Funktions-IDs (öffentliches NvAPI-DRS-Interface)
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdDrsCreateSession = 0x0694D52E;
    private const uint IdDrsDestroySession = 0xDAD9CFF8;
    private const uint IdDrsLoadSettings = 0x375DBD6B;
    private const uint IdDrsGetBaseProfile = 0xDA8466A0;
    private const uint IdDrsFindApplicationByName = 0xEEE566B2;
    private const uint IdDrsGetSetting = 0x73BF8338;

    private const int NvapiOk = 0;
    private const int UnicodeStringChars = 2048; // NvAPI_UnicodeString

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NoArgDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SessionOutDelegate(out IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SessionDelegate(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetBaseProfileDelegate(IntPtr session, out IntPtr profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FindApplicationByNameDelegate(
        IntPtr session, [MarshalAs(UnmanagedType.LPWStr)] string appName,
        out IntPtr profile, ref NvDrsApplication application);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetSettingDelegate(IntPtr session, IntPtr profile, uint settingId, ref NvDrsSetting setting);

    // NVDRS_SETTING_V1: die beiden Wert-Unions (predefined/current) sind je 4100
    // Bytes groß (größtes Mitglied NVDRS_BINARY_SETTING); für u32-Settings liegt
    // der Wert in den ersten 4 Bytes.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct NvDrsSetting
    {
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringChars)]
        public string SettingName;
        public uint SettingId;
        public uint SettingType;
        public uint SettingLocation;
        public uint IsCurrentPredefined;
        public uint IsPredefinedValid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4100)]
        public byte[] PredefinedValue;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4100)]
        public byte[] CurrentValue;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct NvDrsApplication
    {
        public uint Version;
        public uint IsPredefined;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringChars)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringChars)]
        public string UserFriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringChars)]
        public string Launcher;
    }

    /// <summary>
    /// Prüft, ob RTX HDR für die angegebene Exe (Anwendungsprofil) oder global
    /// (Basisprofil) aktiviert ist. null = nicht feststellbar (kein NVIDIA-Treiber,
    /// alte nvapi ohne DRS, Strukturfehler).
    /// </summary>
    public static bool? IsRtxHdrEnabled(string? exeFileName)
    {
        try
        {
            return Query(exeFileName);
        }
        catch (DllNotFoundException)
        {
            return null; // kein NVIDIA-Treiber installiert
        }
        catch (Exception ex)
        {
            Logger.Warn("RTX-HDR-Erkennung fehlgeschlagen: " + ex.Message);
            return null;
        }
    }

    private static bool? Query(string? exeFileName)
    {
        var initialize = GetFunction<NoArgDelegate>(IdInitialize);
        var unload = GetFunction<NoArgDelegate>(IdUnload);
        var createSession = GetFunction<SessionOutDelegate>(IdDrsCreateSession);
        var destroySession = GetFunction<SessionDelegate>(IdDrsDestroySession);
        var loadSettings = GetFunction<SessionDelegate>(IdDrsLoadSettings);
        var getBaseProfile = GetFunction<GetBaseProfileDelegate>(IdDrsGetBaseProfile);
        var findApplication = GetFunction<FindApplicationByNameDelegate>(IdDrsFindApplicationByName);
        var getSetting = GetFunction<GetSettingDelegate>(IdDrsGetSetting);
        if (initialize is null || createSession is null || destroySession is null ||
            loadSettings is null || getBaseProfile is null || findApplication is null ||
            getSetting is null)
        {
            return null; // DRS-Interface nicht verfügbar
        }

        if (initialize() != NvapiOk) return null; // z. B. keine NVIDIA-GPU aktiv
        try
        {
            if (createSession(out IntPtr session) != NvapiOk) return null;
            try
            {
                if (loadSettings(session) != NvapiOk) return null;

                // 1) Anwendungsprofil des Spiels
                if (!string.IsNullOrWhiteSpace(exeFileName))
                {
                    var app = new NvDrsApplication
                    {
                        Version = MakeVersion<NvDrsApplication>(1),
                        AppName = string.Empty, UserFriendlyName = string.Empty, Launcher = string.Empty
                    };
                    // Auf volle Puffergröße auffüllen: nvapi erwartet eine
                    // NvAPI_UnicodeString (fixe 2048 WCHARs), kein kürzeres Array.
                    string padded = exeFileName.ToLowerInvariant().PadRight(UnicodeStringChars - 1, '\0');
                    if (findApplication(session, padded, out IntPtr appProfile, ref app) == NvapiOk &&
                        ReadTrueHdr(getSetting, session, appProfile) == true)
                    {
                        return true;
                    }
                }

                // 2) Basisprofil (globaler Standard)
                if (getBaseProfile(session, out IntPtr baseProfile) == NvapiOk &&
                    ReadTrueHdr(getSetting, session, baseProfile) == true)
                {
                    return true;
                }

                return false;
            }
            finally
            {
                destroySession(session);
            }
        }
        finally
        {
            unload?.Invoke();
        }
    }

    /// <summary>
    /// Liest den RTX-HDR-Status eines Profils: zuerst die TrueHDR-Flags
    /// (NvTrueHDR-Ära), dann das Legacy-Enable (R550-Ära).
    /// null = in diesem Profil nicht gesetzt.
    /// </summary>
    private static bool? ReadTrueHdr(GetSettingDelegate getSetting, IntPtr session, IntPtr profile)
    {
        if (TryGetDword(getSetting, session, profile, TrueHdrFlagsSettingId, out uint flags))
            return (flags & FlagsEnableBit) != 0;
        if (TryGetDword(getSetting, session, profile, LegacyTrueHdrSettingId, out uint legacy))
            return legacy == 1;
        return null;
    }

    private static bool TryGetDword(GetSettingDelegate getSetting, IntPtr session, IntPtr profile,
        uint settingId, out uint value)
    {
        var setting = new NvDrsSetting
        {
            Version = MakeVersion<NvDrsSetting>(1),
            SettingName = string.Empty,
            PredefinedValue = new byte[4100],
            CurrentValue = new byte[4100]
        };
        if (getSetting(session, profile, settingId, ref setting) != NvapiOk)
        {
            value = 0;
            return false;
        }
        value = BitConverter.ToUInt32(setting.CurrentValue, 0);
        return true;
    }

    private static T? GetFunction<T>(uint id) where T : Delegate
    {
        IntPtr ptr = QueryInterface(id);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    /// <summary>NvAPI-Versionsmakro: Strukturgröße | (Version &lt;&lt; 16).</summary>
    private static uint MakeVersion<T>(uint version) =>
        (uint)Marshal.SizeOf<T>() | (version << 16);
}
