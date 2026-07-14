using System.Diagnostics;
using System.Runtime.InteropServices;
using HdrPilot.Models;

namespace HdrPilot.Core;

/// <summary>
/// Liest und setzt NVIDIA RTX HDR (intern "TrueHDR") pro Spiel über das
/// NVIDIA-Treiberprofil (DRS) via nvapi64.dll - derselbe Mechanismus wie
/// NVIDIA App / NvTrueHDR / Profile Inspector.
///
/// Settings (auf Treiber 610.74 verifiziert):
///  - 0x00432F84 "TrueHDR flags": Bit 1 = aktiv, Bit 0 = Bildschirm-Indikator,
///    Qualität: +0x04 = niedrig, +0x08 = mittel, ohne Zusatzbits = sehr hoch.
///  - 0x1077A11A: muss zusätzlich auf 1 stehen (Community/NvTrueHDR).
///  - 0x00DD48FB "Enable TrueHDR" (NVIDIA-App-Ära, R550): in neuen Treibern
///    aus der Setting-Tabelle entfernt - wird nur noch gelesen (alte Treiber).
///
/// WICHTIG: Das Schreiben dieser Settings verlangt seit neueren Treibern
/// Administratorrechte (NVAPI_INVALID_USER_PRIVILEGE ohne Elevation).
/// Deshalb schreibt nicht der Tray-Prozess selbst, sondern ein kurzlebiger
/// elevierter Selbstaufruf ("HdrPilot.exe --apply-rtx ...", eine UAC-Abfrage
/// je Whitelist-Speicherung). Lesen geht ohne Elevation.
///
/// Beim Deaktivieren werden die Settings nur entfernt, wenn RTX HDR aktuell
/// aktiv ist; eine abweichende Wahl aus der NVIDIA App bleibt unangetastet.
/// Die Feinabstimmung (Peak-Helligkeit, Sättigung usw.) bleibt der NVIDIA App
/// überlassen. Alle Aufrufe sind defensiv: ohne NVIDIA-Treiber liefert die
/// Erkennung null ("unbekannt") bzw. es wird nur geloggt.
/// </summary>
internal static class RtxHdrController
{
    private const uint TrueHdrFlagsSettingId = 0x00432F84;
    private const uint TrueHdrAuxSettingId = 0x1077A11A;
    private const uint LegacyTrueHdrSettingId = 0x00DD48FB;

    private const uint FlagsEnableBit = 0x2;
    private const uint FlagsEnabledVeryHigh = 0x2; // aktiv, höchste Qualität, kein Indikator

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
    // Lesen (ohne Elevation möglich)
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
    // Änderungs-Ermittlung und elevierte Anwendung
    // ---------------------------------------------------------------

    /// <summary>
    /// Ermittelt aus einer Whitelist-Änderung, welche Exes RTX HDR bekommen
    /// bzw. verlieren sollen. Die eigentliche Anwendung geschieht eleviert
    /// über <see cref="ApplyElevated"/>.
    /// </summary>
    public static List<(string Exe, bool Enable)> PendingChanges(
        IEnumerable<WhitelistEntry> oldEntries, IEnumerable<WhitelistEntry> newEntries)
    {
        var wanted = ExesWithRtxHdr(newEntries);
        var previous = ExesWithRtxHdr(oldEntries);

        var changes = new List<(string, bool)>();
        foreach (var exe in previous.Except(wanted))
            changes.Add((exe, false));
        foreach (var exe in wanted)
        {
            // Idempotent: nur anfassen, wenn noch nicht aktiv - erspart
            // unnötige UAC-Abfragen beim Speichern unveränderter Listen.
            if (IsRtxHdrEnabled(exe) != true)
                changes.Add((exe, true));
        }
        return changes;
    }

    /// <summary>
    /// Wendet RTX-HDR-Änderungen über einen elevierten Selbstaufruf an
    /// ("HdrPilot.exe --apply-rtx exe|on ..."). Blockiert bis zum Abschluss.
    /// false = UAC abgebrochen oder Anwendung fehlgeschlagen.
    /// </summary>
    public static bool ApplyElevated(IReadOnlyList<(string Exe, bool Enable)> changes)
    {
        if (changes.Count == 0) return true;

        string exePath = Environment.ProcessPath ?? "";
        if (exePath.Length == 0) return false;

        string args = "--apply-rtx " + string.Join(' ',
            changes.Select(c => $"\"{c.Exe}|{(c.Enable ? "on" : "off")}\""));
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true, // nötig für Verb "runas" (UAC)
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (proc is null) return false;
            if (!proc.WaitForExit(120_000)) return false;
            return proc.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Logger.Warn("RTX-HDR-Anwendung abgebrochen (UAC verweigert).");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Elevierter RTX-HDR-Aufruf fehlgeschlagen.", ex);
            return false;
        }
    }

    /// <summary>
    /// Einstiegspunkt des elevierten Hilfsmodus (Argumente nach "--apply-rtx",
    /// Format je Token "exename.exe|on" bzw. "|off"). Rückgabe = Prozess-Exitcode.
    /// </summary>
    public static int RunApplyCli(IEnumerable<string> tokens)
    {
        bool allOk = true;
        foreach (string token in tokens)
        {
            int sep = token.LastIndexOf('|');
            if (sep <= 0) { allOk = false; continue; }
            string exe = token[..sep];
            bool enable = token[(sep + 1)..].Equals("on", StringComparison.OrdinalIgnoreCase);
            try
            {
                if (!SetForApp(exe, enable)) allOk = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"RTX-HDR-Profil-Update fehlgeschlagen für \"{exe}\".", ex);
                allOk = false;
            }
        }
        return allOk ? 0 : 1;
    }

    /// <summary>
    /// Setzt oder entfernt RTX HDR im Anwendungsprofil einer Exe. Erfordert
    /// Administratorrechte (siehe Klassenkommentar). Beim Aktivieren wird das
    /// Profil bei Bedarf angelegt; beim Deaktivieren werden die Settings nur
    /// entfernt, wenn RTX HDR aktuell aktiv ist.
    /// </summary>
    private static bool SetForApp(string exeFileName, bool enable)
    {
        return WithSession<bool?>((nv, session) =>
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
                        return false;
                    }
                }

                if (ReadTrueHdr(nv, session, profile) == true) return true; // schon aktiv

                int stFlags = SetDword(nv, session, profile, TrueHdrFlagsSettingId, FlagsEnabledVeryHigh);
                int stAux = SetDword(nv, session, profile, TrueHdrAuxSettingId, 1);
                if (stFlags != NvapiOk || stAux != NvapiOk)
                {
                    Logger.Error($"RTX HDR: Setting für \"{exeFileName}\" konnte nicht gesetzt werden " +
                                 $"(flags={stFlags}, aux={stAux}).");
                    return false;
                }
                if (nv.SaveSettings(session) != NvapiOk)
                {
                    Logger.Error($"RTX HDR: Speichern der Treiberprofile fehlgeschlagen ({exeFileName}).");
                    return false;
                }
                Logger.Info($"RTX HDR EIN (Treiberprofil): {exeFileName}");
                return true;
            }

            // Deaktivieren: nur zurücknehmen, wenn aktuell aktiv.
            if (!found || ReadTrueHdr(nv, session, profile) != true) return true;

            nv.DeleteProfileSetting(session, profile, TrueHdrFlagsSettingId);
            nv.DeleteProfileSetting(session, profile, TrueHdrAuxSettingId);
            if (nv.SaveSettings(session) != NvapiOk)
            {
                Logger.Error($"RTX HDR: Speichern der Treiberprofile fehlgeschlagen ({exeFileName}).");
                return false;
            }
            Logger.Info($"RTX HDR AUS (Treiberprofil): {exeFileName}");
            return true;
        }) == true;
    }

    private static HashSet<string> ExesWithRtxHdr(IEnumerable<WhitelistEntry> entries) =>
        entries.Where(e => e.EnableRtxHdr && !string.IsNullOrWhiteSpace(e.ExeFileName))
               .Select(e => e.ExeFileName!)
               .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Liest den RTX-HDR-Status eines Profils: zuerst die TrueHDR-Flags
    /// (aktuelle Treiber), dann das Legacy-Enable (ältere Treiber).
    /// null = in diesem Profil nicht gesetzt.
    /// </summary>
    private static bool? ReadTrueHdr(NvApi nv, IntPtr session, IntPtr profile)
    {
        if (TryGetDword(nv, session, profile, TrueHdrFlagsSettingId, out uint flags))
            return (flags & FlagsEnableBit) != 0;
        if (TryGetDword(nv, session, profile, LegacyTrueHdrSettingId, out uint legacy))
            return legacy == 1;
        return null;
    }

    private static bool TryGetDword(NvApi nv, IntPtr session, IntPtr profile, uint settingId, out uint value)
    {
        var setting = NewSetting();
        if (nv.GetSetting(session, profile, settingId, ref setting) != NvapiOk)
        {
            value = 0;
            return false;
        }
        value = BitConverter.ToUInt32(setting.CurrentValue, 0);
        return true;
    }

    private static int SetDword(NvApi nv, IntPtr session, IntPtr profile, uint settingId, uint value)
    {
        var setting = NewSetting();
        setting.SettingId = settingId;
        setting.SettingType = DrsDwordType;
        BitConverter.GetBytes(value).CopyTo(setting.CurrentValue, 0);
        return nv.SetSetting(session, profile, ref setting);
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
