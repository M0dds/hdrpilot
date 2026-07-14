using System.Globalization;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Schlanke Code-basierte Lokalisierung (DE/EN/FR/ES).
/// Bewusst keine .resx-Satellitenassemblies, damit der Single-File-Publish
/// eine einzige .exe bleibt. Fallback-Kette: gewählte Sprache -> Englisch -> Key.
/// </summary>
internal static class Loc
{
    /// <summary>Aktiver Sprachcode ("de", "en", "fr", "es").</summary>
    public static string Current { get; private set; } = "en";

    // Nullable, weil die Sprachtabellen weiter unten deklariert sind und statische
    // Initialisierer in Deklarationsreihenfolge laufen. T() fällt auf En zurück.
    private static Dictionary<string, string>? _table;

    /// <summary>
    /// Aktiviert die Sprache aus der Konfiguration.
    /// "system" folgt der Windows-Anzeigesprache (nicht unterstützte Sprachen -> Englisch).
    /// </summary>
    public static void Apply(string configLanguage)
    {
        string code = configLanguage.Equals("system", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant()
            : configLanguage.ToLowerInvariant();

        (Current, _table) = code switch
        {
            "de" => ("de", De),
            "fr" => ("fr", Fr),
            "es" => ("es", Es),
            _ => ("en", En)
        };
    }

    /// <summary>Liefert den übersetzten Text zu einem Key.</summary>
    public static string T(string key) =>
        (_table ?? En).TryGetValue(key, out var s) ? s
        : En.TryGetValue(key, out var e) ? e
        : key;

    // ---------------------------------------------------------------- Englisch
    private static readonly Dictionary<string, string> En = new()
    {
        ["common.on"] = "On",
        ["common.off"] = "Off",
        ["common.yes"] = "Yes",
        ["common.no"] = "No",
        ["common.ok"] = "OK",
        ["common.cancel"] = "Cancel",

        ["tray.status.detecting"] = "Detecting HDR status…",
        ["tray.status.unavailable"] = "HDR status unavailable",
        ["tray.status.none"] = "HDR: no HDR-capable display",
        ["tray.primaryTag"] = "primary",
        ["tray.menu.whitelist"] = "Manage whitelist…",
        ["tray.menu.settings"] = "Settings…",
        ["tray.menu.forceOff"] = "Turn HDR off now (all displays)",
        ["tray.menu.autostart"] = "Start with Windows",
        ["tray.menu.exit"] = "Exit",
        ["tray.watcherFailed"] = "Process monitoring could not be started: ",

        ["notify.on"] = "HDR enabled",
        ["notify.off"] = "HDR disabled",
        ["notify.allEnded"] = "All programs closed",

        ["wl.title"] = "HDR AutoSwitch – Whitelist",
        ["wl.heading"] = "Whitelist",
        ["wl.hint"] = "Programs that automatically enable HDR. HDR is turned off once all of them have closed.",
        ["wl.col.name"] = "Name",
        ["wl.col.mode"] = "Match",
        ["wl.col.target"] = "Target",
        ["wl.col.active"] = "Active",
        ["wl.col.procPath"] = "Process / path",
        ["wl.btn.addFile"] = "From file…",
        ["wl.btn.addRunning"] = "From running…",
        ["wl.btn.edit"] = "Edit…",
        ["wl.btn.remove"] = "Remove",
        ["wl.btn.save"] = "Save && apply",
        ["wl.allMonitors"] = "All HDR displays",

        ["mode.name"] = "Name",
        ["mode.path"] = "Path",
        ["mode.namePath"] = "Name+path",

        ["dlg.pickExe"] = "Select a program",
        ["dlg.exeFilter"] = "Programs (*.exe)|*.exe|All files (*.*)|*.*",

        ["entry.title"] = "Edit entry",
        ["entry.displayName"] = "Display name:",
        ["entry.processName"] = "Process name:",
        ["entry.path"] = "Path to .exe:",
        ["entry.match"] = "Match by:",
        ["entry.active"] = "Entry active",
        ["entry.switchOn"] = "Switch HDR on:",
        ["entry.allHdr"] = "All HDR-capable displays",
        ["entry.selected"] = "Selected displays only:",
        ["entry.noHdr"] = "(no HDR)",
        ["entry.mode.name"] = "Process name",
        ["entry.mode.path"] = "Full path",
        ["entry.mode.namePath"] = "Name or path (recommended)",
        ["entry.incompleteTitle"] = "Incomplete",
        ["entry.incompleteMsg"] = "Please provide at least a process name or a path.",

        ["picker.title"] = "Select a running program",
        ["picker.filter"] = "Filter:",
        ["picker.col.process"] = "Process",
        ["picker.col.path"] = "Path",
        ["picker.select"] = "Select",
        ["picker.noPath"] = "(path not readable)",

        ["set.title"] = "Settings",
        ["set.appearance"] = "Appearance",
        ["set.language"] = "Language:",
        ["set.theme"] = "Theme:",
        ["theme.system"] = "System",
        ["theme.light"] = "Light",
        ["theme.dark"] = "Dark",
        ["set.behavior"] = "Behavior",
        ["set.notify"] = "Show a notification when switching",
        ["set.restore"] = "Restore the previous HDR state after programs close",
        ["set.target"] = "Switch HDR on:",
        ["target.primary"] = "Primary display only",
        ["target.all"] = "All HDR-capable displays",
        ["set.onDelay"] = "Turn-on delay (ms):",
        ["set.offDelay"] = "Turn-off delay (ms):",
        ["set.save"] = "Save",
    };

    // ---------------------------------------------------------------- Deutsch
    private static readonly Dictionary<string, string> De = new()
    {
        ["common.on"] = "Ein",
        ["common.off"] = "Aus",
        ["common.yes"] = "Ja",
        ["common.no"] = "Nein",
        ["common.ok"] = "OK",
        ["common.cancel"] = "Abbrechen",

        ["tray.status.detecting"] = "HDR-Status wird ermittelt…",
        ["tray.status.unavailable"] = "HDR-Status nicht abrufbar",
        ["tray.status.none"] = "HDR: kein HDR-fähiger Monitor",
        ["tray.primaryTag"] = "primär",
        ["tray.menu.whitelist"] = "Whitelist verwalten…",
        ["tray.menu.settings"] = "Einstellungen…",
        ["tray.menu.forceOff"] = "HDR jetzt aus (alle Monitore)",
        ["tray.menu.autostart"] = "Bei Windows-Start starten",
        ["tray.menu.exit"] = "Beenden",
        ["tray.watcherFailed"] = "Prozessüberwachung konnte nicht gestartet werden: ",

        ["notify.on"] = "HDR aktiviert",
        ["notify.off"] = "HDR deaktiviert",
        ["notify.allEnded"] = "Alle Programme beendet",

        ["wl.title"] = "HDR AutoSwitch – Whitelist",
        ["wl.heading"] = "Whitelist",
        ["wl.hint"] = "Programme, die HDR automatisch aktivieren. HDR wird ausgeschaltet, sobald alle beendet sind.",
        ["wl.col.name"] = "Name",
        ["wl.col.mode"] = "Erkennung",
        ["wl.col.target"] = "Ziel",
        ["wl.col.active"] = "Aktiv",
        ["wl.col.procPath"] = "Prozess / Pfad",
        ["wl.btn.addFile"] = "Aus Datei…",
        ["wl.btn.addRunning"] = "Aus laufenden…",
        ["wl.btn.edit"] = "Bearbeiten…",
        ["wl.btn.remove"] = "Entfernen",
        ["wl.btn.save"] = "Speichern && Anwenden",
        ["wl.allMonitors"] = "Alle HDR-Monitore",

        ["mode.name"] = "Name",
        ["mode.path"] = "Pfad",
        ["mode.namePath"] = "Name+Pfad",

        ["dlg.pickExe"] = "Programm auswählen",
        ["dlg.exeFilter"] = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*",

        ["entry.title"] = "Eintrag bearbeiten",
        ["entry.displayName"] = "Anzeigename:",
        ["entry.processName"] = "Prozessname:",
        ["entry.path"] = "Pfad zur .exe:",
        ["entry.match"] = "Erkennung:",
        ["entry.active"] = "Eintrag aktiv",
        ["entry.switchOn"] = "HDR schalten auf:",
        ["entry.allHdr"] = "Alle HDR-fähigen Monitore",
        ["entry.selected"] = "Nur ausgewählte Monitore:",
        ["entry.noHdr"] = "(kein HDR)",
        ["entry.mode.name"] = "Prozessname",
        ["entry.mode.path"] = "Voller Pfad",
        ["entry.mode.namePath"] = "Name oder Pfad (empfohlen)",
        ["entry.incompleteTitle"] = "Unvollständig",
        ["entry.incompleteMsg"] = "Bitte mindestens einen Prozessnamen oder einen Pfad angeben.",

        ["picker.title"] = "Laufendes Programm auswählen",
        ["picker.filter"] = "Filter:",
        ["picker.col.process"] = "Prozess",
        ["picker.col.path"] = "Pfad",
        ["picker.select"] = "Auswählen",
        ["picker.noPath"] = "(Pfad nicht lesbar)",

        ["set.title"] = "Einstellungen",
        ["set.appearance"] = "Darstellung",
        ["set.language"] = "Sprache:",
        ["set.theme"] = "Design:",
        ["theme.system"] = "System",
        ["theme.light"] = "Hell",
        ["theme.dark"] = "Dunkel",
        ["set.behavior"] = "Verhalten",
        ["set.notify"] = "Benachrichtigung beim Umschalten anzeigen",
        ["set.restore"] = "Vorherigen HDR-Zustand nach Programmende wiederherstellen",
        ["set.target"] = "HDR schalten auf:",
        ["target.primary"] = "Nur Primärmonitor",
        ["target.all"] = "Alle HDR-fähigen Monitore",
        ["set.onDelay"] = "Einschalt-Verzögerung (ms):",
        ["set.offDelay"] = "Ausschalt-Verzögerung (ms):",
        ["set.save"] = "Speichern",
    };

    // ---------------------------------------------------------------- Französisch
    private static readonly Dictionary<string, string> Fr = new()
    {
        ["common.on"] = "Activé",
        ["common.off"] = "Désactivé",
        ["common.yes"] = "Oui",
        ["common.no"] = "Non",
        ["common.ok"] = "OK",
        ["common.cancel"] = "Annuler",

        ["tray.status.detecting"] = "Détection de l'état HDR…",
        ["tray.status.unavailable"] = "État HDR indisponible",
        ["tray.status.none"] = "HDR : aucun écran compatible HDR",
        ["tray.primaryTag"] = "principal",
        ["tray.menu.whitelist"] = "Gérer la liste blanche…",
        ["tray.menu.settings"] = "Paramètres…",
        ["tray.menu.forceOff"] = "Désactiver le HDR maintenant (tous les écrans)",
        ["tray.menu.autostart"] = "Lancer au démarrage de Windows",
        ["tray.menu.exit"] = "Quitter",
        ["tray.watcherFailed"] = "Impossible de démarrer la surveillance des processus : ",

        ["notify.on"] = "HDR activé",
        ["notify.off"] = "HDR désactivé",
        ["notify.allEnded"] = "Tous les programmes sont fermés",

        ["wl.title"] = "HDR AutoSwitch – Liste blanche",
        ["wl.heading"] = "Liste blanche",
        ["wl.hint"] = "Programmes qui activent automatiquement le HDR. Le HDR est désactivé dès qu'ils sont tous fermés.",
        ["wl.col.name"] = "Nom",
        ["wl.col.mode"] = "Détection",
        ["wl.col.target"] = "Cible",
        ["wl.col.active"] = "Actif",
        ["wl.col.procPath"] = "Processus / chemin",
        ["wl.btn.addFile"] = "Depuis un fichier…",
        ["wl.btn.addRunning"] = "Processus en cours…",
        ["wl.btn.edit"] = "Modifier…",
        ["wl.btn.remove"] = "Supprimer",
        ["wl.btn.save"] = "Enregistrer et appliquer",
        ["wl.allMonitors"] = "Tous les écrans HDR",

        ["mode.name"] = "Nom",
        ["mode.path"] = "Chemin",
        ["mode.namePath"] = "Nom+chemin",

        ["dlg.pickExe"] = "Choisir un programme",
        ["dlg.exeFilter"] = "Programmes (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",

        ["entry.title"] = "Modifier l'entrée",
        ["entry.displayName"] = "Nom affiché :",
        ["entry.processName"] = "Nom du processus :",
        ["entry.path"] = "Chemin du .exe :",
        ["entry.match"] = "Détection :",
        ["entry.active"] = "Entrée active",
        ["entry.switchOn"] = "Activer le HDR sur :",
        ["entry.allHdr"] = "Tous les écrans compatibles HDR",
        ["entry.selected"] = "Uniquement les écrans sélectionnés :",
        ["entry.noHdr"] = "(pas de HDR)",
        ["entry.mode.name"] = "Nom du processus",
        ["entry.mode.path"] = "Chemin complet",
        ["entry.mode.namePath"] = "Nom ou chemin (recommandé)",
        ["entry.incompleteTitle"] = "Incomplet",
        ["entry.incompleteMsg"] = "Veuillez indiquer au moins un nom de processus ou un chemin.",

        ["picker.title"] = "Choisir un programme en cours d'exécution",
        ["picker.filter"] = "Filtre :",
        ["picker.col.process"] = "Processus",
        ["picker.col.path"] = "Chemin",
        ["picker.select"] = "Sélectionner",
        ["picker.noPath"] = "(chemin illisible)",

        ["set.title"] = "Paramètres",
        ["set.appearance"] = "Apparence",
        ["set.language"] = "Langue :",
        ["set.theme"] = "Thème :",
        ["theme.system"] = "Système",
        ["theme.light"] = "Clair",
        ["theme.dark"] = "Sombre",
        ["set.behavior"] = "Comportement",
        ["set.notify"] = "Afficher une notification lors du changement",
        ["set.restore"] = "Restaurer l'état HDR précédent après la fermeture des programmes",
        ["set.target"] = "Activer le HDR sur :",
        ["target.primary"] = "Écran principal uniquement",
        ["target.all"] = "Tous les écrans compatibles HDR",
        ["set.onDelay"] = "Délai d'activation (ms) :",
        ["set.offDelay"] = "Délai de désactivation (ms) :",
        ["set.save"] = "Enregistrer",
    };

    // ---------------------------------------------------------------- Spanisch
    private static readonly Dictionary<string, string> Es = new()
    {
        ["common.on"] = "Activado",
        ["common.off"] = "Desactivado",
        ["common.yes"] = "Sí",
        ["common.no"] = "No",
        ["common.ok"] = "Aceptar",
        ["common.cancel"] = "Cancelar",

        ["tray.status.detecting"] = "Detectando el estado HDR…",
        ["tray.status.unavailable"] = "Estado HDR no disponible",
        ["tray.status.none"] = "HDR: ningún monitor compatible con HDR",
        ["tray.primaryTag"] = "principal",
        ["tray.menu.whitelist"] = "Administrar la lista blanca…",
        ["tray.menu.settings"] = "Configuración…",
        ["tray.menu.forceOff"] = "Desactivar HDR ahora (todos los monitores)",
        ["tray.menu.autostart"] = "Iniciar con Windows",
        ["tray.menu.exit"] = "Salir",
        ["tray.watcherFailed"] = "No se pudo iniciar la supervisión de procesos: ",

        ["notify.on"] = "HDR activado",
        ["notify.off"] = "HDR desactivado",
        ["notify.allEnded"] = "Todos los programas cerrados",

        ["wl.title"] = "HDR AutoSwitch – Lista blanca",
        ["wl.heading"] = "Lista blanca",
        ["wl.hint"] = "Programas que activan HDR automáticamente. HDR se desactiva cuando todos se hayan cerrado.",
        ["wl.col.name"] = "Nombre",
        ["wl.col.mode"] = "Detección",
        ["wl.col.target"] = "Destino",
        ["wl.col.active"] = "Activo",
        ["wl.col.procPath"] = "Proceso / ruta",
        ["wl.btn.addFile"] = "Desde archivo…",
        ["wl.btn.addRunning"] = "Desde procesos…",
        ["wl.btn.edit"] = "Editar…",
        ["wl.btn.remove"] = "Quitar",
        ["wl.btn.save"] = "Guardar y aplicar",
        ["wl.allMonitors"] = "Todos los monitores HDR",

        ["mode.name"] = "Nombre",
        ["mode.path"] = "Ruta",
        ["mode.namePath"] = "Nombre+ruta",

        ["dlg.pickExe"] = "Seleccionar un programa",
        ["dlg.exeFilter"] = "Programas (*.exe)|*.exe|Todos los archivos (*.*)|*.*",

        ["entry.title"] = "Editar entrada",
        ["entry.displayName"] = "Nombre para mostrar:",
        ["entry.processName"] = "Nombre del proceso:",
        ["entry.path"] = "Ruta al .exe:",
        ["entry.match"] = "Detección:",
        ["entry.active"] = "Entrada activa",
        ["entry.switchOn"] = "Activar HDR en:",
        ["entry.allHdr"] = "Todos los monitores compatibles con HDR",
        ["entry.selected"] = "Solo los monitores seleccionados:",
        ["entry.noHdr"] = "(sin HDR)",
        ["entry.mode.name"] = "Nombre del proceso",
        ["entry.mode.path"] = "Ruta completa",
        ["entry.mode.namePath"] = "Nombre o ruta (recomendado)",
        ["entry.incompleteTitle"] = "Incompleto",
        ["entry.incompleteMsg"] = "Indica al menos un nombre de proceso o una ruta.",

        ["picker.title"] = "Seleccionar un programa en ejecución",
        ["picker.filter"] = "Filtro:",
        ["picker.col.process"] = "Proceso",
        ["picker.col.path"] = "Ruta",
        ["picker.select"] = "Seleccionar",
        ["picker.noPath"] = "(ruta no legible)",

        ["set.title"] = "Configuración",
        ["set.appearance"] = "Apariencia",
        ["set.language"] = "Idioma:",
        ["set.theme"] = "Tema:",
        ["theme.system"] = "Sistema",
        ["theme.light"] = "Claro",
        ["theme.dark"] = "Oscuro",
        ["set.behavior"] = "Comportamiento",
        ["set.notify"] = "Mostrar una notificación al cambiar",
        ["set.restore"] = "Restaurar el estado HDR anterior al cerrar los programas",
        ["set.target"] = "Activar HDR en:",
        ["target.primary"] = "Solo el monitor principal",
        ["target.all"] = "Todos los monitores compatibles con HDR",
        ["set.onDelay"] = "Retraso de activación (ms):",
        ["set.offDelay"] = "Retraso de desactivación (ms):",
        ["set.save"] = "Guardar",
    };
}
