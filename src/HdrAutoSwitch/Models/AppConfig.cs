namespace HdrAutoSwitch.Models;

/// <summary>
/// Gesamte persistierte Konfiguration. Wird als JSON in %AppData%\HdrAutoSwitch\config.json abgelegt.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Schema-Version für spätere Migrationen.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Alle Whitelist-Einträge.</summary>
    public List<WhitelistEntry> Whitelist { get; set; } = new();

    /// <summary>Mit Windows starten (Registry Run-Key). Wird beim Speichern angewandt.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Beim Beenden des letzten Whitelist-Programms den HDR-Zustand wiederherstellen,
    /// der vor dem ersten Programmstart herrschte. False = HDR eingeschaltet lassen.
    /// </summary>
    public bool RestorePreviousState { get; set; } = true;

    /// <summary>
    /// Auf welchen Monitoren geschaltet wird, wenn ein Eintrag keine eigene Auswahl hat:
    /// nur Primärmonitor (Standard) oder alle HDR-fähigen.
    /// </summary>
    public TargetMode TargetMode { get; set; } = TargetMode.PrimaryOnly;

    /// <summary>
    /// Verzögerung in Millisekunden, bevor bei Prozessende HDR zurückgeschaltet wird.
    /// Verhindert Flackern bei kurzen Neustarts (z. B. Launcher -> Spiel).
    /// </summary>
    public int OffDebounceMs { get; set; } = 1500;

    /// <summary>
    /// Verzögerung in Millisekunden, bevor bei Prozessstart HDR eingeschaltet wird.
    /// Verhindert Flackern, wenn ein Prozess sofort wieder beendet wird.
    /// </summary>
    public int OnDebounceMs { get; set; } = 1500;

    /// <summary>Ballon-/Tray-Benachrichtigung anzeigen, wenn HDR umgeschaltet wird.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Farbschema der Oberfläche: System (Standard), Light oder Dark.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>
    /// UI-Sprache: "system" (folgt Windows), "de", "en", "fr" oder "es".
    /// </summary>
    public string Language { get; set; } = "system";
}
