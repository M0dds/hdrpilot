using System.Runtime.InteropServices;
using HdrAutoSwitch.Models;
using static HdrAutoSwitch.Native.DisplayConfig;

namespace HdrAutoSwitch.Core;

/// <summary>
/// Zentrale Abstraktion über die native DisplayConfig-API.
/// Listet Monitore, liest HDR-Status und schaltet HDR pro Monitor.
///
/// Primär wird die Windows-11-24H2-API verwendet (DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE
/// + GET_ADVANCED_COLOR_INFO_2); steht sie nicht zur Verfügung (ältere Builds oder
/// Fehler zur Laufzeit), fällt jeder Aufruf automatisch auf den Legacy-Pfad
/// (SET_ADVANCED_COLOR_STATE + GET_ADVANCED_COLOR_INFO) zurück.
/// </summary>
public sealed class HdrController
{
    /// <summary>True, wenn Windows die 24H2-HDR-API (SET_HDR_STATE) voraussichtlich bereitstellt.</summary>
    public bool UsesModernApi { get; private set; }

    // Monitore, für die bereits eine ACM-Warnung geloggt wurde (nur einmal pro Lauf warnen).
    private readonly HashSet<string> _acmWarned = new(StringComparer.OrdinalIgnoreCase);

    public HdrController()
    {
        // Windows 11 24H2 == Build 26100 oder höher. Zusätzlich fällt jeder einzelne
        // API-Aufruf bei Fehlern auf den Legacy-Pfad zurück (defensive Doppelabsicherung).
        UsesModernApi = Environment.OSVersion.Version.Build >= 26100;
        Logger.Info($"HdrController: Build {Environment.OSVersion.Version.Build}, " +
                    (UsesModernApi ? "24H2-API (SET_HDR_STATE)" : "Legacy-API (SET_ADVANCED_COLOR_STATE)"));
    }

    /// <summary>
    /// Ermittelt alle aktiven Monitore inklusive HDR-Fähigkeit, aktuellem Zustand
    /// und Primärmonitor-Kennung (Quellmodus-Position 0,0).
    /// </summary>
    public List<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();

        int err = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
        if (err != ERROR_SUCCESS || pathCount == 0)
        {
            if (err != ERROR_SUCCESS)
                Logger.Error($"GetDisplayConfigBufferSizes fehlgeschlagen (Win32-Fehler {err}).");
            return result;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        err = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (err != ERROR_SUCCESS)
        {
            Logger.Error($"QueryDisplayConfig fehlgeschlagen (Win32-Fehler {err}).");
            return result;
        }

        for (int i = 0; i < pathCount; i++)
        {
            var source = paths[i].sourceInfo;
            var target = paths[i].targetInfo;

            // Friendly Name + Device Path holen
            var nameReq = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = target.adapterId,
                    id = target.id
                }
            };

            string friendly = "Monitor";
            string devicePath = $"{target.adapterId.HighPart:X}-{target.adapterId.LowPart:X}-{target.id}";
            if (DisplayConfigGetDeviceInfo(ref nameReq) == ERROR_SUCCESS)
            {
                if (!string.IsNullOrWhiteSpace(nameReq.monitorFriendlyDeviceName))
                    friendly = nameReq.monitorFriendlyDeviceName;
                if (!string.IsNullOrWhiteSpace(nameReq.monitorDevicePath))
                    devicePath = nameReq.monitorDevicePath;
            }

            var mon = new MonitorInfo
            {
                FriendlyName = friendly,
                DevicePath = devicePath,
                TargetId = target.id,
                AdapterLow = target.adapterId.LowPart,
                AdapterHigh = target.adapterId.HighPart,
                IsPrimary = IsPrimaryPath(source, modes, modeCount)
            };

            ReadHdrState(target.adapterId, target.id, mon);

            // Duplikate (gleicher Pfad über mehrere Paths, z. B. Clone-Modus) überspringen
            if (!result.Exists(m => m.DevicePath == mon.DevicePath))
                result.Add(mon);
        }

        return result;
    }

    /// <summary>
    /// Der Primärmonitor ist der, dessen Quellmodus an Desktop-Position (0,0) liegt.
    /// </summary>
    private static bool IsPrimaryPath(DISPLAYCONFIG_PATH_SOURCE_INFO source,
        DISPLAYCONFIG_MODE_INFO[] modes, uint modeCount)
    {
        // Bevorzugt über den im Pfad hinterlegten Mode-Index...
        if (source.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && source.modeInfoIdx < modeCount)
        {
            var m = modes[source.modeInfoIdx];
            if (m.infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                return m.sourceMode.position.x == 0 && m.sourceMode.position.y == 0;
        }

        // ...sonst per Suche über Adapter-/Quell-ID.
        for (int i = 0; i < modeCount; i++)
        {
            var m = modes[i];
            if (m.infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE &&
                m.id == source.id &&
                m.adapterId.LowPart == source.adapterId.LowPart &&
                m.adapterId.HighPart == source.adapterId.HighPart)
            {
                return m.sourceMode.position.x == 0 && m.sourceMode.position.y == 0;
            }
        }
        return false;
    }

    private void ReadHdrState(LUID adapterId, uint targetId, MonitorInfo mon)
    {
        if (UsesModernApi)
        {
            var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                    size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };
            int err = DisplayConfigGetDeviceInfo(ref info);
            if (err == ERROR_SUCCESS)
            {
                mon.HdrSupported = info.HdrSupported;
                mon.HdrEnabled = info.HdrUserEnabled;
                mon.AcmActive = info.AcmActive;

                // 24H2 "Auto Color Management": verfälscht ggf. die HDR-Statuserkennung.
                // Nur warnen, nicht blockieren.
                if (info.AcmActive && _acmWarned.Add(mon.DevicePath))
                {
                    Logger.Warn($"ACM (Auto Color Management) ist auf \"{mon.FriendlyName}\" aktiv " +
                                "- die HDR-Statuserkennung kann dadurch verfälscht sein.");
                }
                if (info.AdvancedColorLimitedByPolicy && _acmWarned.Add(mon.DevicePath + "|policy"))
                {
                    Logger.Warn($"HDR auf \"{mon.FriendlyName}\" ist per Richtlinie eingeschränkt " +
                                "(z. B. Energiesparmodus).");
                }
                return;
            }

            // Moderne API nicht verfügbar (z. B. gemeldeter 24H2-Build ohne Funktion):
            // dauerhaft auf Legacy umschalten.
            Logger.Warn($"GET_ADVANCED_COLOR_INFO_2 fehlgeschlagen (Win32-Fehler {err}) - wechsle auf Legacy-API.");
            UsesModernApi = false;
        }

        var legacy = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                adapterId = adapterId,
                id = targetId
            }
        };
        if (DisplayConfigGetDeviceInfo(ref legacy) == ERROR_SUCCESS)
        {
            mon.HdrSupported = legacy.AdvancedColorSupported && !legacy.AdvancedColorForceDisabled;
            mon.HdrEnabled = legacy.AdvancedColorEnabled;
        }
    }

    /// <summary>
    /// Schaltet HDR für einen bestimmten Monitor. Gibt true bei Erfolg zurück.
    /// </summary>
    public bool SetHdr(MonitorInfo mon, bool enable)
    {
        var adapterId = new LUID { LowPart = mon.AdapterLow, HighPart = mon.AdapterHigh };

        if (UsesModernApi)
        {
            var req = new DISPLAYCONFIG_SET_HDR_STATE
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE,
                    size = Marshal.SizeOf<DISPLAYCONFIG_SET_HDR_STATE>(),
                    adapterId = adapterId,
                    id = mon.TargetId
                },
                value = enable ? 1u : 0u
            };
            int err = DisplayConfigSetDeviceInfo(ref req);
            if (err == ERROR_SUCCESS)
            {
                mon.HdrEnabled = enable;
                Logger.Info($"HDR {(enable ? "EIN" : "AUS")} (24H2-API): {mon.FriendlyName}");
                return true;
            }
            // Bei Fehler auf Legacy zurückfallen (manche Treiber melden 24H2, verhalten sich aber alt).
            Logger.Warn($"SET_HDR_STATE fehlgeschlagen (Win32-Fehler {err}) - versuche Legacy-API.");
        }

        var legacyReq = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                size = Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = adapterId,
                id = mon.TargetId
            },
            value = enable ? 1u : 0u
        };
        int legacyErr = DisplayConfigSetDeviceInfo(ref legacyReq);
        if (legacyErr == ERROR_SUCCESS)
        {
            mon.HdrEnabled = enable;
            Logger.Info($"HDR {(enable ? "EIN" : "AUS")} (Legacy-API): {mon.FriendlyName}");
            return true;
        }

        Logger.Error($"HDR-Umschaltung fehlgeschlagen für \"{mon.FriendlyName}\" (Win32-Fehler {legacyErr}).");
        return false;
    }

    /// <summary>
    /// Schaltet HDR auf einer Menge von Monitoren.
    /// Leere Ziel-Liste = Auswahl gemäß <paramref name="mode"/>
    /// (Standard: nur Primärmonitor). Monitore, die bereits im Zielzustand sind,
    /// werden übersprungen - so wird HDR nur eingeschaltet, wenn es aus war.
    /// Gibt die Liste der Monitore zurück, die tatsächlich umgeschaltet wurden.
    /// </summary>
    public List<MonitorInfo> SetHdrOnTargets(IReadOnlyCollection<string> targetDevicePaths, bool enable,
        TargetMode mode = TargetMode.AllHdrCapable)
    {
        var changed = new List<MonitorInfo>();
        var monitors = GetMonitors();

        IEnumerable<MonitorInfo> selection;
        if (targetDevicePaths.Count > 0)
            selection = monitors.Where(m => m.HdrSupported && targetDevicePaths.Contains(m.DevicePath));
        else if (mode == TargetMode.PrimaryOnly)
            selection = monitors.Where(m => m.HdrSupported && m.IsPrimary);
        else
            selection = monitors.Where(m => m.HdrSupported);

        var list = selection.ToList();
        if (list.Count == 0 && enable)
        {
            Logger.Warn(mode == TargetMode.PrimaryOnly && targetDevicePaths.Count == 0
                ? "Kein HDR-fähiger Primärmonitor gefunden - nichts zu schalten."
                : "Kein passender HDR-fähiger Monitor gefunden - nichts zu schalten.");
        }

        foreach (var mon in list)
        {
            if (mon.HdrEnabled == enable) continue; // schon im Zielzustand
            if (SetHdr(mon, enable))
                changed.Add(mon);
        }
        return changed;
    }
}
