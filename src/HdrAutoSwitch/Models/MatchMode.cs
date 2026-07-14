namespace HdrAutoSwitch.Models;

/// <summary>
/// Legt fest, wie ein laufender Prozess mit einem Whitelist-Eintrag abgeglichen wird.
/// </summary>
public enum MatchMode
{
    /// <summary>Nur der Prozessname wird verglichen (z. B. "cyberpunk2077.exe"). Groß-/Kleinschreibung egal.</summary>
    ProcessName,

    /// <summary>Nur der vollständige Pfad zur ausführbaren Datei wird verglichen.</summary>
    FullPath,

    /// <summary>Treffer, wenn Prozessname ODER voller Pfad passt (großzügigste Variante).</summary>
    NameOrPath
}
