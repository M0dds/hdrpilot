namespace HdrPilot.Models;

/// <summary>
/// Legt fest, auf welchen Monitoren HDR geschaltet wird, wenn ein Whitelist-Eintrag
/// keine eigene Monitorauswahl trifft.
/// </summary>
public enum TargetMode
{
    /// <summary>Nur der Primärmonitor (Desktop-Position 0,0). Standard.</summary>
    PrimaryOnly,

    /// <summary>Alle Monitore, die HDR unterstützen.</summary>
    AllHdrCapable
}
