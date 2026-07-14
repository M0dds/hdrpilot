using System.Text.Json;
using Microsoft.Win32;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.Core;

/// <summary>
/// Lädt und speichert die <see cref="AppConfig"/> als JSON in
/// %AppData%\HdrAutoSwitch\config.json und verwaltet den Windows-Autostart.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "HdrAutoSwitch";

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }

    public ConfigStore()
    {
        ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HdrAutoSwitch");
        ConfigPath = Path.Combine(ConfigDirectory, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg is not null)
                    return cfg;
            }
        }
        catch
        {
            // Beschädigte Konfig -> mit Defaults starten, statt abzustürzen.
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        string json = JsonSerializer.Serialize(config, JsonOpts);
        // Atomar schreiben: erst temp, dann ersetzen.
        string tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, ConfigPath, overwrite: true);
        File.Delete(tmp);

        ApplyAutostart(config.StartWithWindows);
    }

    /// <summary>Trägt die App in den Run-Key ein oder entfernt sie (kein Admin nötig, HKCU).</summary>
    private static void ApplyAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enable)
            {
                string exe = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(RunValueName, $"\"{exe}\" --background");
            }
            else
            {
                if (key.GetValue(RunValueName) is not null)
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Autostart ist optional; Fehler hier darf die App nicht stören.
        }
    }
}
