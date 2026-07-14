using System.Runtime.InteropServices;
using HdrPilot.Models;

namespace HdrPilot.Core;

/// <summary>
/// Liest und setzt NVIDIA RTX HDR (intern "TrueHDR") pro Spiel über das
/// NVIDIA-Treiberprofil (DRS) via nvapi64.dll - derselbe Mechanismus, den die
/// NVIDIA App nutzt: Setting 0x00DD48FB = "Enable TrueHDR Feature", 1 = aktiv.
///
/// Zuordnung erfolgt über den Exe-Dateinamen (Anwendungsprofil); existiert für
/// ein Spiel noch kein Profil, wird eines angelegt. Beim Deaktivieren wird das
/// Setting nur entfernt, wenn es aktuell aktiv (1) ist - eine anderslautende
/// Wahl aus der NVIDIA App bleibt unangetastet.
///
/// Geschrieben wird beim Speichern der Whitelist (nicht erst beim Prozessstart):
/// der Treiber liest das Profil beim Spielstart. Die Feinabstimmung
/// (Peak-Helligkeit, Sättigung usw.) bleibt der NVIDIA App überlassen.
///
/// Alle Aufrufe sind defensiv: ohne NVIDIA-Treiber oder bei unerwarteten
/// Strukturen liefert die Erkennung null ("unbekannt") bzw. loggt nur.
/// </summary>
internal static class RtxHdrController
{
    private const uint TrueHdrSettingId = 0x00DD48FB;
    private const uint DrsDwordType = 0; // NVDRS_DWORD_TYPE

    // nvapi_QueryInterface-Funktions-IDs (öffentliches NvAPI-DRS-Interface)
    private const uint IdInitialize = 0x0150E828;
    private const uint IdUnload = 0xD22BDD7E;
    private const uint IdDrsCreateSession = 0x0694D52E;
    private const uint IdDrsDestroySession = 0xDAD9CFF8;
    private const uint IdDrsLoadSettings = 0x375DBD6B;
    private const uint IdDrsSaveSettings = 0xFCBC7E14;
    private const uint IdDrsGetBaseProfile = 0xDA8466A0;
    private const uint IdDrsFindProfileByName = 0x7E4A9A0B;
    private const uint IdDrsCreateProfile = 0xCC176068;
    private const uint IdDrsFindApplicationByName = 0xEEE566B2;
    private const uint IdDrsCreateApplication = 0x4347A9DE;
    private const uint IdDrsGetSetting = 0x73BF8338;
    private const uint IdDrsSetSetting = 0x577DD202;
    private const uint IdDrsDeleteProfileSetting = 0xE4A26362;

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
    private delegate int FindProfileByNameDelegate(
        IntPtr session, [MarshalAs(UnmanagedType.LPWStr)] string profileName, out IntPtr profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateProfileDelegate(IntPtr session, ref NvDrsProfile profile, out IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FindApplicationByNameDelegate(
        IntPtr session, [MarshalAs(UnmanagedType.LPWStr)] string appName,
        out IntPtr profile, ref NvDrsApplication application);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateApplicationDelegate(IntPtr session, IntPtr profile, ref NvDrsApplication application);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetSettingDelegate(IntPtr session, IntPtr profile, uint settingId, ref NvDrsSetting setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetSettingDelegate(IntPtr session, IntPtr profile, ref NvDrsSetting setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DeleteProfileSettingDelegate(IntPtr session, IntPtr profile, uint settingId);

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

    // NVDRS_PROFILE_V1: gpuSupport ist ein u32-Bitfeld; 0 = Standard (wie im
    // NVIDIA-SDK-Beispiel beim Anlegen eigener Profile).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct NvDrsProfile
    {
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UnicodeStringChars)]
        public string ProfileName;
        public uint GpuSupport;
        public uint IsPredefined;
        public uint NumOfApps;
        public uint NumOfSettings;
    }

    // ---------------------------------------------------------------
    // Lesen
    // ---------------------------------------------------------------

    /// <summary>
    /// Prüft, ob RTX HDR für die angegebene Exe (Anwendungsprofil) oder global
    /// (Basisprofil) aktiviert ist. null = nicht feststellbar (kein NVIDIA-Treiber,
    /// alte nvapi ohne DRS, Strukturfehler).
    /// </summary>
    public static bool? IsRtxHdrEnabled(string? exeFileName)
    {
        try
        {
            return WithSession<bool?>((nv, session) =>
            {
                // 1) Anwendungsprofil des Spiels (NVIDIA App: "Spiel-spezifisch")
                if (!string.IsNullOrWhiteSpace(exeFileName) &&
                    TryFindAppProfile(nv, session, exeFileName, out IntPtr appProfile) &&
                    ReadTrueHdr(nv, session, appProfile) == true)
                {
                    return true;
                }

                // 2) Basisprofil (NVIDIA App: globaler Standard)
                if (nv.GetBaseProfile(session, out IntPtr baseProfile) == NvapiOk &&
                    ReadTrueHdr(nv, session, baseProfile) == true)
                {
                    return true;
                }

                return false;
            });
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

    /// <summary>Grobe Verfügbarkeitsprüfung: NVIDIA-Treiber mit DRS-Interface nutzbar?</summary>
    public static bool IsNvapiAvailable()
    {
        try
        {
            return WithSession<bool?>((_, _) => true) == true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------
    // Schreiben
    // ---------------------------------------------------------------

    /// <summary>
    /// Setzt oder entfernt RTX HDR im Anwendungsprofil einer Exe.
    /// Beim Aktivieren wird das Profil bei Bedarf angelegt; beim Deaktivieren
    /// wird das Setting nur entfernt, wenn es aktuell aktiv (1) ist.
    /// </summary>
    public static void SetForApp(string exeFileName, bool enable)
    {
        WithSession<object?>((nv, session) =>
        {
            bool found = TryFindAppProfile(nv, session, exeFileName, out IntPtr profile);

            if (enable)
            {
                if (!found)
                {
                    profile = CreateAppProfile(nv, session, exeFileName);
                    if (profile == IntPtr.Zero)
                    {
                        Logger.Error($"RTX HDR: Profil für \"{exeFileName}\" konnte nicht angelegt werden.");
                        return null;
                    }
                }

                if (ReadTrueHdr(nv, session, profile) == true) return null; // schon aktiv

                var setting = NewSetting();
                setting.SettingId = TrueHdrSettingId;
                setting.SettingType = DrsDwordType;
                BitConverter.GetBytes(1u).CopyTo(setting.CurrentValue, 0);

                if (nv.SetSetting(session, profile, ref setting) != NvapiOk)
                {
                    Logger.Error($"RTX HDR: Setting für \"{exeFileName}\" konnte nicht gesetzt werden.");
                    return null;
                }
                if (nv.SaveSettings(session) == NvapiOk)
                    Logger.Info($"RTX HDR EIN (Treiberprofil): {exeFileName}");
                return null;
            }

            // Deaktivieren: nur zurücknehmen, was aktiv auf 1 steht.
            if (!found || ReadTrueHdr(nv, session, profile) != true) return null;

            if (nv.DeleteProfileSetting(session, profile, TrueHdrSettingId) == NvapiOk &&
                nv.SaveSettings(session) == NvapiOk)
            {
                Logger.Info($"RTX HDR AUS (Treiberprofil): {exeFileName}");
            }
            return null;
        });
    }

    /// <summary>
    /// Gleicht die Treiberprofile mit einer Whitelist-Änderung ab: Exes, die
    /// RTX HDR verlieren, werden zurückgesetzt; alle gewünschten (idempotent) gesetzt.
    /// </summary>
    public static void ApplyWhitelistChange(IEnumerable<WhitelistEntry> oldEntries, IEnumerable<WhitelistEntry> newEntries)
    {
        var wanted = ExesWithRtxHdr(newEntries);
        var previous = ExesWithRtxHdr(oldEntries);

        foreach (var exe in previous.Except(wanted))
            Try(() => SetForApp(exe, enable: false), exe);
        foreach (var exe in wanted)
            Try(() => SetForApp(exe, enable: true), exe);
    }

    /// <summary>
    /// Beim App-Start: stellt sicher, dass alle konfigurierten Einträge ihr
    /// Treiberprofil-Setting haben (z. B. nach Treiber-Neuinstallation).
    /// Entfernt nichts.
    /// </summary>
    public static void EnsureApplied(IEnumerable<WhitelistEntry> entries)
    {
        foreach (var exe in ExesWithRtxHdr(entries))
            Try(() => SetForApp(exe, enable: true), exe);
    }

    private static HashSet<string> ExesWithRtxHdr(IEnumerable<WhitelistEntry> entries) =>
        entries.Where(e => e.EnableRtxHdr && !string.IsNullOrWhiteSpace(e.ExeFileName))
               .Select(e => e.ExeFileName!)
               .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void Try(Action action, string exe)
    {
        try { action(); }
        catch (DllNotFoundException) { /* kein NVIDIA-Treiber - still ignorieren */ }
        catch (Exception ex) { Logger.Error($"RTX-HDR-Profil-Update fehlgeschlagen für \"{exe}\".", ex); }
    }

    // ---------------------------------------------------------------
    // DRS-Hilfen
    // ---------------------------------------------------------------

    /// <summary>Gebündelte DRS-Funktionszeiger einer nvapi-Sitzung.</summary>
    private sealed record NvApi(
        SessionDelegate SaveSettings,
        GetBaseProfileDelegate GetBaseProfile,
        FindProfileByNameDelegate FindProfileByName,
        CreateProfileDelegate CreateProfile,
        FindApplicationByNameDelegate FindApplicationByName,
        CreateApplicationDelegate CreateApplication,
        GetSettingDelegate GetSetting,
        SetSettingDelegate SetSetting,
        DeleteProfileSettingDelegate DeleteProfileSetting);

    /// <summary>
    /// Initialisiert nvapi, öffnet eine DRS-Session mit geladenen Settings und
    /// führt <paramref name="body"/> aus. Räumt Session und nvapi wieder ab.
    /// Liefert default, wenn das DRS-Interface nicht verfügbar ist.
    /// </summary>
    private static T? WithSession<T>(Func<NvApi, IntPtr, T> body)
    {
        var initialize = GetFunction<NoArgDelegate>(IdInitialize);
        var unload = GetFunction<NoArgDelegate>(IdUnload);
        var createSession = GetFunction<SessionOutDelegate>(IdDrsCreateSession);
        var destroySession = GetFunction<SessionDelegate>(IdDrsDestroySession);
        var loadSettings = GetFunction<SessionDelegate>(IdDrsLoadSettings);
        var saveSettings = GetFunction<SessionDelegate>(IdDrsSaveSettings);
        var getBaseProfile = GetFunction<GetBaseProfileDelegate>(IdDrsGetBaseProfile);
        var findProfileByName = GetFunction<FindProfileByNameDelegate>(IdDrsFindProfileByName);
        var createProfile = GetFunction<CreateProfileDelegate>(IdDrsCreateProfile);
        var findApplication = GetFunction<FindApplicationByNameDelegate>(IdDrsFindApplicationByName);
        var createApplication = GetFunction<CreateApplicationDelegate>(IdDrsCreateApplication);
        var getSetting = GetFunction<GetSettingDelegate>(IdDrsGetSetting);
        var setSetting = GetFunction<SetSettingDelegate>(IdDrsSetSetting);
        var deleteProfileSetting = GetFunction<DeleteProfileSettingDelegate>(IdDrsDeleteProfileSetting);

        if (initialize is null || createSession is null || destroySession is null ||
            loadSettings is null || saveSettings is null || getBaseProfile is null ||
            findProfileByName is null || createProfile is null || findApplication is null ||
            createApplication is null || getSetting is null || setSetting is null ||
            deleteProfileSetting is null)
        {
            return default; // DRS-Interface nicht verfügbar
        }

        var nv = new NvApi(saveSettings, getBaseProfile, findProfileByName, createProfile,
            findApplication, createApplication, getSetting, setSetting, deleteProfileSetting);

        if (initialize() != NvapiOk) return default; // z. B. keine NVIDIA-GPU aktiv
        try
        {
            if (createSession(out IntPtr session) != NvapiOk) return default;
            try
            {
                if (loadSettings(session) != NvapiOk) return default;
                return body(nv, session);
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

    /// <summary>Sucht das Anwendungsprofil einer Exe (Match über den Exe-Namen, lowercase).</summary>
    private static bool TryFindAppProfile(NvApi nv, IntPtr session, string exeFileName, out IntPtr profile)
    {
        var app = new NvDrsApplication
        {
            Version = MakeVersion<NvDrsApplication>(1),
            AppName = string.Empty, UserFriendlyName = string.Empty, Launcher = string.Empty
        };
        return nv.FindApplicationByName(session, PadUnicode(exeFileName.ToLowerInvariant()),
            out profile, ref app) == NvapiOk;
    }

    /// <summary>
    /// Legt ein Anwendungsprofil für eine Exe an. Kollidiert der Profilname mit
    /// einem vorhandenen Profil (ohne passende Exe-Zuordnung), wird dieses wiederverwendet.
    /// </summary>
    private static IntPtr CreateAppProfile(NvApi nv, IntPtr session, string exeFileName)
    {
        string exeLower = exeFileName.ToLowerInvariant();

        var profileInfo = new NvDrsProfile
        {
            Version = MakeVersion<NvDrsProfile>(1),
            ProfileName = exeLower
        };
        int status = nv.CreateProfile(session, ref profileInfo, out IntPtr profile);
        if (status != NvapiOk)
        {
            // Name evtl. schon vergeben (z. B. vordefiniertes Profil): wiederverwenden.
            if (nv.FindProfileByName(session, PadUnicode(exeLower), out profile) != NvapiOk)
                return IntPtr.Zero;
        }

        var app = new NvDrsApplication
        {
            Version = MakeVersion<NvDrsApplication>(1),
            AppName = exeLower, UserFriendlyName = string.Empty, Launcher = string.Empty
        };
        if (nv.CreateApplication(session, profile, ref app) != NvapiOk)
            return IntPtr.Zero;

        return profile;
    }

    /// <summary>Liest das TrueHDR-Setting eines Profils; null = Setting nicht vorhanden/lesbar.</summary>
    private static bool? ReadTrueHdr(NvApi nv, IntPtr session, IntPtr profile)
    {
        var setting = NewSetting();
        if (nv.GetSetting(session, profile, TrueHdrSettingId, ref setting) != NvapiOk)
            return null; // Setting im Profil nicht gesetzt
        return BitConverter.ToUInt32(setting.CurrentValue, 0) == 1;
    }

    private static NvDrsSetting NewSetting() => new()
    {
        Version = MakeVersion<NvDrsSetting>(1),
        SettingName = string.Empty,
        PredefinedValue = new byte[4100],
        CurrentValue = new byte[4100]
    };

    /// <summary>
    /// Auf volle Puffergröße auffüllen: nvapi erwartet eine NvAPI_UnicodeString
    /// (fixe 2048 WCHARs), kein kürzeres Array.
    /// </summary>
    private static string PadUnicode(string s) => s.PadRight(UnicodeStringChars - 1, '\0');

    private static T? GetFunction<T>(uint id) where T : Delegate
    {
        IntPtr ptr = QueryInterface(id);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    /// <summary>NvAPI-Versionsmakro: Strukturgröße | (Version &lt;&lt; 16).</summary>
    private static uint MakeVersion<T>(uint version) =>
        (uint)Marshal.SizeOf<T>() | (version << 16);
}
