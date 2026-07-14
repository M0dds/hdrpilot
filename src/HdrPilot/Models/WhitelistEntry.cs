using System.Text.Json.Serialization;

namespace HdrPilot.Models;

/// <summary>
/// Ein einzelner Eintrag der Whitelist: welches Programm HDR auslöst und auf welchen Monitoren.
/// </summary>
public sealed class WhitelistEntry
{
    /// <summary>Anzeigename in der UI (frei wählbar, rein kosmetisch).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Prozessname inkl. Endung, z. B. "cyberpunk2077.exe".
    /// Wird bei <see cref="MatchMode.ProcessName"/> und <see cref="MatchMode.NameOrPath"/> genutzt.
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// Vollständiger Pfad zur .exe. Wird bei <see cref="MatchMode.FullPath"/>
    /// und <see cref="MatchMode.NameOrPath"/> genutzt.
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>Wie abgeglichen wird.</summary>
    public MatchMode MatchMode { get; set; } = MatchMode.NameOrPath;

    /// <summary>Ob dieser Eintrag aktiv ist. Deaktivierte Einträge werden ignoriert.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Stabile Hardware-IDs (DevicePath) der Monitore, auf denen HDR geschaltet werden soll.
    /// Leere Liste = alle HDR-fähigen Monitore.
    /// </summary>
    public List<string> TargetMonitorIds { get; set; } = new();

    /// <summary>
    /// Windows Auto-HDR für dieses Programm aktivieren (Per-App-Grafikeinstellung).
    /// Benötigt <see cref="FullPath"/>, da Windows die Einstellung pro Exe-Pfad speichert.
    /// Standard: aus.
    /// </summary>
    public bool EnableAutoHdr { get; set; } = false;

    /// <summary>
    /// Prüft, ob ein laufender Prozess auf diesen Eintrag passt.
    /// </summary>
    /// <param name="processName">Prozessname inkl. ".exe" (z. B. aus WMI).</param>
    /// <param name="fullPath">Vollständiger Pfad zur .exe, kann null sein wenn nicht ermittelbar.</param>
    public bool Matches(string processName, string? fullPath)
    {
        if (!Enabled) return false;

        bool nameHit =
            !string.IsNullOrWhiteSpace(ProcessName) &&
            processName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase);

        bool pathHit =
            !string.IsNullOrWhiteSpace(FullPath) &&
            fullPath is not null &&
            fullPath.Equals(FullPath, StringComparison.OrdinalIgnoreCase);

        return MatchMode switch
        {
            MatchMode.ProcessName => nameHit,
            MatchMode.FullPath => pathHit,
            MatchMode.NameOrPath => nameHit || pathHit,
            _ => false
        };
    }

    /// <summary>Ein eindeutiger Schlüssel, um Einträge im Referenzzähler zu unterscheiden.</summary>
    [JsonIgnore]
    public string Key => $"{MatchMode}|{ProcessName}|{FullPath}";
}
