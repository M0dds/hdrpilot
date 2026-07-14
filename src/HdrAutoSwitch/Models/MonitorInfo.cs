namespace HdrAutoSwitch.Models;

/// <summary>
/// Ein vom System erkannter, aktiver Monitor mit seinen HDR-Eigenschaften.
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>Lesbarer Name, z. B. "Dell U2723QE". Für die UI.</summary>
    public string FriendlyName { get; set; } = "Unbenannter Monitor";

    /// <summary>
    /// Stabiler Hardware-Pfad des Monitors. Dient als ID in der Whitelist,
    /// bleibt über Neustarts/Umstöpseln hinweg gleich (anders als der reine Index).
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>Ob der Monitor HDR grundsätzlich unterstützt.</summary>
    public bool HdrSupported { get; set; }

    /// <summary>Ob HDR aktuell aktiv ist.</summary>
    public bool HdrEnabled { get; set; }

    /// <summary>Ob dies der Primärmonitor ist (Desktop-Position 0,0).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Ob Windows "Auto Color Management" (ACM, ab 24H2) auf diesem Monitor aktiv ist.</summary>
    public bool AcmActive { get; set; }

    // Interne Adapter-/Ziel-IDs für den Set-Aufruf (nicht persistiert).
    internal uint TargetId { get; set; }
    internal uint AdapterLow { get; set; }
    internal int AdapterHigh { get; set; }

    public override string ToString() =>
        $"{FriendlyName} ({(HdrSupported ? (HdrEnabled ? "HDR an" : "HDR aus") : "kein HDR")})";
}
