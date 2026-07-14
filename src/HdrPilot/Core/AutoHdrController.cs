using Microsoft.Win32;
using HdrPilot.Models;

namespace HdrPilot.Core;

/// <summary>
/// Verwaltet die Windows-Auto-HDR-Einstellung pro Anwendung über denselben
/// Registry-Mechanismus wie die Windows-Grafikeinstellungen:
/// HKCU\Software\Microsoft\DirectX\UserGpuPreferences, Wertname = voller Exe-Pfad,
/// Inhalt = Token-Liste wie "GpuPreference=2;AutoHDREnable=2097;".
///
/// AutoHDREnable: Bit 0 = eingeschaltet. Windows 11 24H2 schreibt für die
/// Per-App-Auswahl "Ein" den Wert 2097 (Enable- plus Override-Flags); dieselbe
/// Kodierung nutzt auch diese App. Die Per-App-Einstellung übersteuert den
/// globalen Auto-HDR-Schalter (DirectXUserGlobalSettings).
///
/// Geschrieben wird beim Speichern der Whitelist (nicht erst beim Prozessstart):
/// Auto-HDR wird vom Spiel beim Start gelesen, das WMI-Startereignis käme zu spät.
/// </summary>
internal static class AutoHdrController
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string GlobalValueName = "DirectXUserGlobalSettings";
    private const string TokenName = "AutoHDREnable";
    private const uint EnabledValue = 2097; // Kodierung der Windows-Settings-Auswahl "Ein" (24H2)

    /// <summary>
    /// Globaler Auto-HDR-Schalter aus den Windows-Einstellungen.
    /// null = Wert nicht vorhanden oder nicht lesbar.
    /// </summary>
    public static bool? IsGlobalAutoHdrEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(GlobalValueName) is not string prefs) return null;
            return TryGetToken(prefs, out uint value) ? (value & 1) != 0 : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Liest, ob Auto-HDR für eine Exe per Registry aktiviert ist.</summary>
    public static bool IsEnabledForApp(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(exePath) is not string prefs) return false;
            return TryGetToken(prefs, out uint value) && (value & 1) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Setzt oder entfernt das Auto-HDR-Token für eine Exe. Andere Tokens im
    /// selben Wert (z. B. GpuPreference) bleiben erhalten. Beim Entfernen wird
    /// nur unser eigener Wert (2097) angefasst - eine abweichende, manuell in
    /// den Windows-Einstellungen getroffene Wahl bleibt unangetastet.
    /// </summary>
    public static void SetForApp(string exePath, bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (key is null) return;

        string prefs = key.GetValue(exePath) as string ?? string.Empty;
        bool hasToken = TryGetToken(prefs, out uint current);

        if (enable)
        {
            if (hasToken && current == EnabledValue) return; // schon gesetzt
            string merged = RemoveToken(prefs) + $"{TokenName}={EnabledValue};";
            key.SetValue(exePath, merged);
            Logger.Info($"Auto-HDR EIN (Registry): {exePath}");
        }
        else
        {
            if (!hasToken || current != EnabledValue) return; // nicht von uns gesetzt
            string remaining = RemoveToken(prefs);
            if (remaining.Length == 0)
                key.DeleteValue(exePath, throwOnMissingValue: false);
            else
                key.SetValue(exePath, remaining);
            Logger.Info($"Auto-HDR AUS (Registry): {exePath}");
        }
    }

    /// <summary>
    /// Gleicht die Registry mit einer Whitelist-Änderung ab: Pfade, die Auto-HDR
    /// verlieren (Haken entfernt oder Eintrag gelöscht), werden zurückgesetzt;
    /// alle gewünschten Pfade werden (idempotent) gesetzt.
    /// </summary>
    public static void ApplyWhitelistChange(IEnumerable<WhitelistEntry> oldEntries, IEnumerable<WhitelistEntry> newEntries)
    {
        var wanted = PathsWithAutoHdr(newEntries);
        var previous = PathsWithAutoHdr(oldEntries);

        foreach (var path in previous.Except(wanted))
            Try(() => SetForApp(path, enable: false), path);
        foreach (var path in wanted)
            Try(() => SetForApp(path, enable: true), path);
    }

    /// <summary>
    /// Beim App-Start: stellt sicher, dass alle konfigurierten Einträge ihr
    /// Registry-Token haben (z. B. nach Neuinstallation von Windows-Grafiktreibern).
    /// Entfernt nichts.
    /// </summary>
    public static void EnsureApplied(IEnumerable<WhitelistEntry> entries)
    {
        foreach (var path in PathsWithAutoHdr(entries))
            Try(() => SetForApp(path, enable: true), path);
    }

    private static HashSet<string> PathsWithAutoHdr(IEnumerable<WhitelistEntry> entries) =>
        entries.Where(e => e.EnableAutoHdr && !string.IsNullOrWhiteSpace(e.FullPath))
               .Select(e => e.FullPath!)
               .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void Try(Action action, string path)
    {
        try { action(); }
        catch (Exception ex) { Logger.Error($"Auto-HDR-Registry-Update fehlgeschlagen für \"{path}\".", ex); }
    }

    /// <summary>Sucht das AutoHDREnable-Token in einer "Name=Wert;"-Liste.</summary>
    private static bool TryGetToken(string prefs, out uint value)
    {
        foreach (var part in prefs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            if (!part.AsSpan(0, eq).Trim().Equals(TokenName, StringComparison.OrdinalIgnoreCase)) continue;
            if (uint.TryParse(part.AsSpan(eq + 1), out value)) return true;
        }
        value = 0;
        return false;
    }

    /// <summary>Entfernt das AutoHDREnable-Token, alle anderen Tokens bleiben in Reihenfolge erhalten.</summary>
    private static string RemoveToken(string prefs)
    {
        var kept = prefs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p =>
            {
                int eq = p.IndexOf('=');
                return eq <= 0 || !p.AsSpan(0, eq).Trim().Equals(TokenName, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        return kept.Count == 0 ? string.Empty : string.Join(';', kept) + ";";
    }
}
