using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Localization;

/// <summary>
/// All texts shown to the user (German). Kept in one place so a second language can be added later by
/// replacing this class with a resource lookup. Technical exception messages from Windows stay as they are.
/// </summary>
public static class Texts
{
    // MIDI connection
    public static string MidiListFailed(string reason) => $"MIDI-Geräte konnten nicht aufgelistet werden: {reason}";

    public static string MidiDisconnected(string device) => $"{device} wurde getrennt. Warte auf Wiederverbindung.";

    public static string MidiNotFound(string device) => $"{device} nicht gefunden. Bitte per USB anschließen.";

    public static string MidiConnected(string device) => $"Verbunden mit {device}.";

    public static string MidiOpenFailed(string device, string reason) =>
        $"{device} wurde gefunden, konnte aber nicht geöffnet werden (nutzt ein anderes Programm das Gerät?): {reason}";

    public static string MidiDriverWarning(int rawMessage) => $"Der MIDI-Treiber meldete eine ungültige Nachricht (0x{rawMessage:X8}).";

    // Audio
    public const string AppNotSet = "Es ist keine Anwendung ausgewählt.";

    public static string AppSilentDeferred(string app) =>
        $"{app} gibt gerade keinen Ton aus. Der Wert wird gemerkt und angewendet, sobald die Anwendung startet.";

    public static string AppSessionsUpdated(string app, int count) => $"{app}: {count} Audiositzung(en) angepasst.";

    public static string AppSessionsFailed(string app) =>
        $"{app}: Die Audiositzungen konnten nicht geändert werden (eventuell wurden sie gerade beendet).";

    public const string TargetSelectedDevice = "Das ausgewählte Gerät";

    public const string TargetDefaultRecording = "Das Standard-Aufnahmegerät";

    public const string TargetDefaultPlayback = "Das Standard-Wiedergabegerät";

    public static string TargetQueryFailed(string label, string reason) => $"{label} konnte nicht abgefragt werden: {reason}";

    public const string NoRecordingDevice = "Kein Standard-Aufnahmegerät verfügbar. Bitte ein Mikrofon anschließen oder aktivieren.";

    public static string TargetUnavailable(string label) => $"{label} ist nicht verfügbar.";

    public const string WhatVolume = "Lautstärke";

    public const string WhatMute = "Stummschaltung";

    public static string EndpointChanged(string device, string what) => $"{device}: {what} geändert.";

    public static string EndpointUnsupported(string device, string what, string reason) =>
        $"{device} erlaubt keine {what}-Steuerung über Windows ({reason}).";

    public static string EndpointDisconnected(string device) => $"{device} wurde gerade getrennt.";

    public static string EndpointFailed(string device, string what, string reason) =>
        $"{device}: {what} konnte nicht geändert werden ({reason}).";

    public static string WindowsReported(int hresult) => $"Windows meldet 0x{hresult:X8}";

    public const string DeviceOrSessionGone = "Das Gerät oder die Sitzung ist nicht mehr vorhanden.";

    // Actions
    public const string AudioActionFailed = "Diese Audio-Aktion konnte nicht ausgeführt werden.";

    public const string ActionUnsupported = "Diese Art von Aktion wird nicht unterstützt.";

    public static string ActionFailed(string reason) => $"Die Aktion ist fehlgeschlagen: {reason}";

    public const string ActionNotTriggered = "Diese Aktion reagiert nicht auf diese Bedienung (zum Beispiel auf das Loslassen einer Taste).";

    public static string MediaSent(MediaCommand command) => $"Medientaste gesendet: {MediaCommandName(command)}.";

    public static string HotkeySent(string hotkey) => $"Tastenkombination gesendet: {hotkey}.";

    public static string MediaCommandName(MediaCommand command) => command switch
    {
        MediaCommand.PlayPause => "Wiedergabe/Pause",
        MediaCommand.Next => "Nächster Titel",
        MediaCommand.Previous => "Vorheriger Titel",
        MediaCommand.Stop => "Stopp",
        _ => command.ToString(),
    };

    public static string KeyInputBlocked(uint sent, int total, int error) =>
        $"Windows hat die Tasteneingabe blockiert ({sent} von {total} Ereignissen, Fehler {error}). " +
        "Möglicherweise hat ein Programm mit Administratorrechten den Fokus.";

    // Shortcuts
    public const string HotkeyEmpty = "Die Tastenkombination ist leer.";

    public static string HotkeyEmptyPart(string text) => $"'{text}' enthält einen leeren Teil (ein '+' ohne Taste daneben).";

    public static string HotkeyUnknownKey(string token) => $"Unbekannte Taste '{token}'.";

    public static string HotkeyTwoKeys(string text) =>
        $"'{text}' enthält mehr als eine Taste. Verwenden Sie Modifikatoren (Strg, Alt, Umschalt, Win) plus genau eine Taste.";

    public const string HotkeyNeedsKey = "Zur Tastenkombination fehlt eine Taste (Modifikatoren allein reichen nicht).";

    // Profiles and files
    public const string ProfileNameEmpty = "Der Profilname ist leer.";

    public static string ControlMappedTwice(ControlLabel control) => $"{control} ist mehrfach belegt.";

    public static string ActionIncomplete(ControlLabel control) =>
        $"{control}: Die Aktion ist unvollständig (Ziel, Gerät oder Tasten fehlen).";

    public static string MappingWithoutAction(ControlLabel control) => $"{control}: Die Belegung enthält keine Aktion.";

    public static string ActionNotAllowed(ControlLabel control, string action, ControlShape shape) => shape == ControlShape.Button
        ? $"{control}: {action} passt nicht zu einer Taste."
        : $"{control}: {action} passt nicht zu einem stufenlosen Regler (Fader oder Drehregler).";

    public const string KindNameVolume = "Lautstärke";

    public const string KindNameMute = "Stummschaltung";

    public const string KindNameMedia = "Medienbefehl";

    public const string KindNameHotkey = "Tastenkombination";

    public const string KindNameSwitch = "Gerätewechsel";

    public const string KindNameUnknown = "Diese Aktion";

    public static string ScalingInvalid(ControlLabel control) =>
        $"{control}: Der Wertebereich muss 0 ≤ Minimum ≤ Maximum ≤ 1 erfüllen.";

    public const string ProfileFileInvalid = "Die Profildatei ist kein gültiges JSON oder hat einen unerwarteten Aufbau.";

    public const string ProfileFileEmpty = "Die Profildatei ist leer.";

    public static string ProfileFileVersion(int found, int supported) =>
        $"Nicht unterstützte Version {found} der Profildatei (dieses Programm unterstützt bis Version {supported}).";

    public static string ProfileLoadFailed(string reason, string note) =>
        $"Die Profile konnten nicht geladen werden ({reason}). Es werden Standardwerte verwendet. {note}";

    public static string ProfileBackupKept(string path) => $"Die alte Datei wurde als '{path}' gesichert.";

    public const string ProfileBackupFailed = "Die alte Datei konnte nicht gesichert werden.";

    // User interface
    public const string UiSearchingDevice = "Suche nach dem Gerät …";

    public const string UiLearnHint = "Bewegen oder drücken Sie jetzt einen Regler am Gerät. Abbrechen mit Esc oder der Schaltfläche.";

    public static string UiLearned(string control) => $"Regler erkannt: {control}.";

    public static string UiExtraControl(string control) => $"Weiterer Regler ({control})";

    public const string UiNoMapping = "nicht belegt";

    public static string UiMappingSaved(string control, string summary) => $"Zuordnung gespeichert: {control} → {summary}.";

    public static string UiMappingRemoved(string control) => $"Zuordnung entfernt: {control}.";

    public static string UiSaveFailed(string reason) => $"Die Profile konnten nicht gespeichert werden: {reason}";

    public static string UiProfileNameTaken(string name) => $"Es gibt bereits ein Profil mit dem Namen '{name}'.";

    public const string UiProfileNameEmpty = "Der Profilname darf nicht leer sein.";

    public const string UiLastProfile = "Das letzte Profil kann nicht gelöscht werden.";

    public static string UiNewProfileName(int number) => $"Profil {number}";

    public const string UiEditorAppRequired = "Bitte den Namen der Anwendung angeben, zum Beispiel Discord.exe.";

    public const string UiEditorRangeInvalid = "Das Minimum darf nicht größer als das Maximum sein.";

    public static string UiAutostartFailed(string reason) => $"Der Autostart konnte nicht geändert werden: {reason}";

    public static string UiSettingsSaveFailed(string reason) => $"Die Einstellungen konnten nicht gespeichert werden: {reason}";

    public static string UiActionResultFor(string control, string message) => $"{control}: {message}";

    public static string DescribeVolume(string target) => $"Lautstärke: {target}";

    public static string DescribeMute(string target) => $"Stumm schalten: {target}";

    public static string DescribeMuteWhileHeld(string target) => $"Stumm solange gedrückt: {target}";

    public static string DescribeUnmuteWhileHeld(string target) => $"Ton solange gedrückt: {target}";

    /// <summary>Short description of a mapping with several actions: the first one plus how many more.</summary>
    public static string DescribeMore(string first, int more) => $"{first} (+{more})";

    public static string DescribeMedia(MediaCommand command) => $"Medien: {MediaCommandName(command)}";

    public static string DescribeHotkey(string keys) => $"Tasten: {keys}";

    public const string TargetSpeakersShort = "Lautsprecher";

    public const string TargetMicrophoneShort = "Mikrofon";

    public const string TargetDeviceShort = "Gerät";

    public const string UiKindNone = "Keine Aktion";

    public const string UiKindVolume = "Lautstärke regeln";

    public const string UiKindMute = "Stummschaltung umschalten";

    public const string UiKindMedia = "Medienbefehl";

    public const string UiKindHotkey = "Tastenkombination";

    public const string UiTargetSpeakers = "Lautsprecher (Standard-Wiedergabegerät)";

    public const string UiTargetMicrophone = "Mikrofon (Standard-Aufnahmegerät)";

    public const string UiTargetApplication = "Anwendung";

    public static string AudioUnavailable(string reason) =>
        $"Der Windows-Audiodienst ist nicht verfügbar, Lautstärke-Aktionen funktionieren nicht: {reason}";

    public static string UnexpectedError(string reason) => $"Unerwarteter Fehler: {reason}. Einzelheiten stehen in der Datei error.log neben den Profilen.";

    public const string TrayOpen = "Öffnen";

    public const string TrayExit = "Beenden";

    public static string TrayTooltip(string connection) => $"Ear Saxophone – {connection}";

    // Device switching
    public static string DeviceKindName(DeviceFlow flow) => flow == DeviceFlow.Capture ? "Aufnahmegerät" : "Wiedergabegerät";

    public static string SwitchNoDevices(DeviceFlow flow) => $"Es ist kein {DeviceKindName(flow)} zum Umschalten verfügbar.";

    public static string SwitchNeedTwo(DeviceFlow flow) =>
        $"Zum Umschalten werden mindestens zwei {DeviceKindName(flow)}e gebraucht (angeschlossen und, falls ausgewählt, in der Auswahl enthalten).";

    public static string SwitchDone(DeviceFlow flow, string name) => $"{DeviceKindName(flow)}: {name}";

    public static string SwitchRefused(string name, string reason) => $"Windows hat das Umschalten auf {name} abgelehnt ({reason}).";

    public static string DescribeSwitch(DeviceFlow flow, int selected) =>
        selected == 0 ? $"{DeviceKindName(flow)} wechseln" : $"{DeviceKindName(flow)} wechseln ({selected} ausgewählt)";

    public const string UiKindSwitch = "Audiogerät wechseln";

    public const string UiSwitchRender = "Wiedergabe (Lautsprecher, Kopfhörer)";

    public const string UiSwitchCapture = "Aufnahme (Mikrofon)";

    public const string UiSwitchHint = "Jeder Tastendruck schaltet zum nächsten Gerät. Ohne Haken werden alle angeschlossenen Geräte durchgeschaltet.";

    public const string UiNoDevicesListed = "Keine Geräte gefunden.";

    // On-screen display
    public const string OsdTitleMedia = "Medien";

    public const string OsdMuted = "Stumm";

    public const string OsdUnmuted = "Ton an";

    public const string OsdInactive = "nicht aktiv";

    public const string UiOsdToggle = "Anzeige bei Änderungen";

    public const string UiOsdTest = "Anzeige testen";

    public static string OsdPositionName(Settings.OsdPosition position) => position switch
    {
        Settings.OsdPosition.TopLeft => "Oben links",
        Settings.OsdPosition.TopCenter => "Oben Mitte",
        Settings.OsdPosition.TopRight => "Oben rechts",
        Settings.OsdPosition.BottomLeft => "Unten links",
        Settings.OsdPosition.BottomCenter => "Unten Mitte",
        _ => "Unten rechts",
    };

    public const string SwitchInterfaceMissing = "Die Windows-Schnittstelle zum Umschalten ist auf diesem System nicht verfügbar";
}

/// <summary>Display text of a control, used inside messages.</summary>
public readonly record struct ControlLabel(string Text)
{
    public override string ToString() => Text;
}
