# HDR AutoSwitch

Eine schlanke Windows-Tray-Anwendung, die HDR automatisch aktiviert, sobald ein
Programm aus einer selbst gepflegten Whitelist läuft – und HDR wieder ausschaltet,
sobald alle diese Programme beendet sind.

Läuft als Standard-Nutzer (kein Admin, kein Dienst, kein UAC). HDR wird über die
native Windows-`DisplayConfig`-API geschaltet – zuverlässig und unabhängig von der
Xbox Game Bar (anders als die Tastenkombination `Win + Alt + B`).

## Funktionen

- **Automatisches HDR pro Programm** über eine Whitelist.
- **Erkennung wahlweise** über Prozessname (case-insensitive), vollen Pfad oder beides.
- **Ziel-Monitore konfigurierbar**: global „Nur Primärmonitor" (Standard) oder
  „Alle HDR-fähigen" (`TargetMode` in der config.json); zusätzlich kann jeder
  Whitelist-Eintrag eigene Monitore ansteuern.
- **Zustands-Wiederherstellung**: HDR wird nur eingeschaltet, wo es aus war.
  Sobald kein Whitelist-Programm mehr läuft (bzw. beim Beenden der App), wird der
  vorherige Zustand wiederhergestellt (abschaltbar über `RestorePreviousState`).
- **Referenzzählung + Debounce (1500 ms)**: Mehrere gleichzeitige Programme halten
  HDR zusammen; kurze Start/Stop-Zyklen (Launcher → Spiel) verursachen kein Flackern.
- **Ereignisbasierte Prozessüberwachung** via WMI-Abos – kein Polling durch die App.
  Ohne Adminrechte (`Win32_ProcessStartTrace` erfordert sie) wechselt die App
  automatisch auf `__InstanceCreation/DeletionEvent (WITHIN 2)`.
- **Autostart mit Windows** optional (HKCU Run-Key, Toggle direkt im Tray-Menü).
- **Hell- und Dunkelmodus** (folgt standardmäßig der Windows-Einstellung,
  umschaltbar in den Einstellungen) inkl. dunkler Titelleiste.
- **Mehrsprachig**: Deutsch, Englisch, Französisch, Spanisch – Standard ist die
  Windows-Anzeigesprache, umstellbar in den Einstellungen.
- **Einstellungsdialog** im Tray-Menü: Sprache, Design, Autostart,
  Benachrichtigungen, Zustands-Wiederherstellung, Ziel-Monitore, Debounce-Zeiten.
- **Logging** nach `%AppData%\HdrAutoSwitch\log.txt` (rotiert bei ~1 MB).

## Voraussetzungen

- Windows 11 (empfohlen 24H2 / Build 26100+). Auf älteren Win11-Builds wird
  automatisch der Legacy-API-Pfad verwendet. Windows 10 wird nicht unterstützt.
- HDR-fähiger Monitor + GPU.
- .NET 8 SDK zum Bauen (`dotnet --version` ≥ 8).

## Bauen

### In VS Code

1. Ordner in VS Code öffnen (C#-Dev-Kit-Erweiterung empfohlen).
2. `Terminal → Run Build Task` (`Strg+Umschalt+B`) → **build**.
3. Zum Starten `F5` (Konfiguration „HDR AutoSwitch (Debug)").

### In der Kommandozeile

```powershell
# Debug-Build
dotnet build HdrAutoSwitch.sln

# Einzelne portable .exe (Release, Framework-abhängig)
dotnet publish src/HdrAutoSwitch/HdrAutoSwitch.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Die fertige `HdrAutoSwitch.exe` liegt danach im Ordner `publish/`.

Für eine Version ohne installierte .NET-Runtime (größer, aber überall lauffähig):

```powershell
dotnet publish src/HdrAutoSwitch/HdrAutoSwitch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Benutzung

1. `HdrAutoSwitch.exe` starten. Beim ersten Start (leere Whitelist) öffnet sich das
   Verwaltungsfenster automatisch. Danach lebt die App im Infobereich (Tray).
2. **Whitelist verwalten** über das Tray-Menü oder Doppelklick auf das Icon.
3. Programme hinzufügen: **Aus Datei…** (die .exe wählen) oder
   **Aus laufenden…** (aus der Liste offener Programme).
4. Pro Eintrag Erkennungsmodus und Ziel-Monitore festlegen.
5. **Speichern & Anwenden**. Ab jetzt schaltet HDR automatisch.
6. Sprache, Design (Hell/Dunkel/System) und Verhalten unter
   **Einstellungen…** im Tray-Menü anpassen.

Start im Hintergrund (für Autostart): `HdrAutoSwitch.exe --background`

## Architektur

```
src/HdrAutoSwitch/
├── Models/          Datentypen (Whitelist, Konfig, Monitor, MatchMode)
├── Native/          P/Invoke-Signaturen für die DisplayConfig-API
├── Core/
│   ├── HdrController      Kapselt die native API (24H2 + Legacy-Fallback)
│   ├── ProcessWatcher     WMI-Ereignisse für Start/Stop + Enumeration
│   ├── ConfigStore        JSON-Persistenz in %AppData% + Autostart
│   └── AutoSwitchEngine    Kernlogik: Referenzzählung, Debounce, Umschalten
├── UI/              Tray-Kontext, Whitelist-Fenster, Dialoge, Icon
└── Program.cs       Einstieg (Single-Instance)
```

Die Konfiguration liegt unter `%AppData%\HdrAutoSwitch\config.json` und ist von
Hand editierbar (u. a. `RestorePreviousState`, `TargetMode`: `PrimaryOnly` |
`AllHdrCapable`, `OnDebounceMs`/`OffDebounceMs`). Das Fehlerprotokoll liegt daneben
in `log.txt`.

## Wichtige Hinweise

### „Automatically manage color for apps" (ACM, ab 24H2)

Ist diese Windows-Option aktiv (`Einstellungen → System → Anzeige → Farbverwaltung`),
kann sich HDR-Erkennung und -Umschaltung anders verhalten, weil Windows SDR-Inhalte
bereits erweitert. Die App erkennt ACM über die 24H2-API, schreibt eine Warnung ins
Log und funktioniert weiter. Bei Problemen mit dem Schalten die Option testweise
deaktivieren.

### SmartScreen / Antivirus

Die selbst gebaute `.exe` ist nicht signiert (Code-Signing-Zertifikate kosten Geld).
Windows SmartScreen zeigt daher beim ersten Start ggf. „Der Computer wurde durch
Windows geschützt" → **Weitere Informationen → Trotzdem ausführen**. Kleine, unsignierte
Tray-Apps, die einen Autostart-Eintrag schreiben und Display-APIs aufrufen, werden von
manchen Virenscannern heuristisch markiert – bei Bedarf eine Ausnahme hinzufügen.

### Was dieses Tool NICHT tut

Es verändert **nicht** die HDR-Tonemapping-Kurve, SDR-Helligkeit oder Farbkalibrierung.
Es schaltet HDR nur an und aus – genau das, was `Win + Alt + B` tut, nur automatisiert
und pro Programm/Monitor steuerbar. Für Kalibrierung bleibt die „Windows HDR Calibration"-App
aus dem Microsoft Store der richtige Weg.

## Lizenz

Frei zur privaten Nutzung und Anpassung.
