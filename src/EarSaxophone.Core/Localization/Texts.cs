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

    public const string UiKindMute = "Stumm schalten";

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

    public const string UiStyle = "Stil";

    public static string StyleName(Settings.AppStyle style) => style switch
    {
        Settings.AppStyle.Studio => "Studio dunkel",
        Settings.AppStyle.Papier => "Papier und Hardware",
        Settings.AppStyle.Minimal => "Weiß minimal",
        Settings.AppStyle.Amber => "Retro Amber",
        Settings.AppStyle.Blueprint => "Blueprint",
        Settings.AppStyle.Sunset => "Sunset",
        Settings.AppStyle.Rack => "Rack Silber",
        _ => "Messing und Holz",
    };

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

    // Editor V2: several targets, mute modes, apply / discard
    public const string UiMuteModeToggle = "Umschalten";

    public const string UiMuteModeMuteWhileHeld = "Gedrückt halten = stumm";

    public const string UiMuteModeUnmuteWhileHeld = "Gedrückt halten = Ton an";

    public static string UiTargetAlreadyListed(string target) => $"{target} steht bereits in der Liste.";

    public const string UiEditorActionIncomplete = "Eine Aktion ist unvollständig (Ziel, Gerät oder Tasten fehlen).";

    public static string UiEditorNotAllowed(string action, ControlShape shape) => shape == ControlShape.Button
        ? $"{action} passt nicht zu einer Taste."
        : $"{action} passt nicht zu einem stufenlosen Regler (Fader oder Drehregler).";

    public static string UiKeptActions(int count) => count == 1
        ? "1 weitere Aktion bleibt unverändert erhalten."
        : $"{count} weitere Aktionen bleiben unverändert erhalten.";

    public static string UiAutoApplied(string control, string summary) => $"Änderungen an {control} automatisch übernommen: {summary}.";

    public static string UiChangesDiscardedInvalid(string control, string reason) =>
        $"Änderungen an {control} verworfen, weil sie unvollständig waren: {reason}";

    public static string UiChangesReverted(string control) => $"Änderungen an {control} verworfen.";

    public static string UiPendingSelection(string touched, string editing) =>
        $"{touched} wurde am Gerät bedient. Erst die Änderungen an {editing} übernehmen oder verwerfen.";

    public const string UiFollowDevice = "Auswahl folgt dem Gerät";

    public const string UiPinSelection = "Auswahl anpinnen";

    // Drag and drop in the picture
    public static string UiTransferMoved(string from, string to) => $"Belegung von {from} nach {to} verschoben.";

    public static string UiTransferSwapped(string from, string to) => $"Belegungen von {from} und {to} getauscht.";

    public static string UiTransferCopied(string from, string to) => $"Belegung von {from} nach {to} kopiert.";

    public static string UiTransferIncompatible(string from, string to) =>
        $"Die Belegung passt nicht zur Art des anderen Reglers ({from} → {to}). Nichts geändert.";

    public static string UiTransferNothing(string from) => $"{from}: nichts zu verschieben.";

    // On-screen display with several targets
    public static string OsdMoreTargets(string first, int more) => $"{first} + {more}";

    // Question for a new program with sound
    public static string PromptPlaysSound(string program) => $"{program} spielt Ton";

    public const string PromptQuestion = "Auf welchen Regler legen?";

    public const string PromptNotNow = "Nicht jetzt";

    public const string PromptNeverAsk = "Nie fragen";

    public const string PromptWindowTitle = "Ear Saxophone Nachfrage";

    public static string UiProgramAssigned(string program, string control) => $"{program} liegt jetzt auf {control}.";

    public static string OsdProgramAssigned(string control) => $"jetzt auf {control}";

    public static string UiProgramIgnored(string program) => $"{program}: Es wird nicht mehr nachgefragt.";

    public const string UiAskForNewPrograms = "Bei neuem Programm mit Ton fragen";

    public const string UiResetIgnoredPrograms = "Ignorierte Programme zurücksetzen";

    public const string UiIgnoredProgramsReset = "Ignorierte Programme zurückgesetzt.";

    // Main window V2 (labels and tooltips of the window; bound with x:Static)
    public const string UiReconnect = "Neu verbinden";

    public const string UiReconnectTip = "Gerät neu öffnen, falls es nach Ruhezustand oder kurzem Abziehen nicht mehr reagiert";

    public const string UiLearn = "MIDI-Learn";

    public const string UiLearnTip = "Danach einen Regler oder eine Taste am Gerät bewegen oder drücken (auch weitere Regler, die nicht im Bild sind)";

    public const string UiCancel = "Abbrechen";

    public const string UiClose = "Schließen";

    public const string UiSettings = "Einstellungen";

    public const string UiOsdPosition = "Position der Anzeige";

    public const string UiStartWithWindows = "Mit Windows starten";

    public const string UiKeepInTray = "Im Infobereich bleiben";

    public const string UiStartMinimized = "Minimiert starten";

    public const string UiProfile = "Profil";

    public const string UiProfileMenu = "Profil verwalten";

    public const string UiProfileRename = "Name des Profils";

    public const string UiProfileRenameTip = "Neuen Namen eingeben und mit Enter bestätigen";

    public const string UiNewProfile = "Neues Profil";

    public const string UiDeleteProfile = "Profil löschen";

    public const string UiSelectPrompt = "Ein Element im Bild anklicken oder am Gerät bedienen.";

    public const string UiDragHint = "Belegung auf ein anderes Element ziehen = verschieben oder tauschen, mit Strg = kopieren";

    public const string UiPendingHint = "Am Gerät bedient:";

    public const string UiSelectPending = "Wechseln";

    public const string UiExtraControls = "Weitere Regler";

    public const string UiActionKind = "Aktion";

    public const string UiTargets = "Was soll gesteuert werden?";

    public const string UiNoTargets = "Noch nichts gewählt. Unten auf „Hinzufügen“ klicken.";

    public const string UiAddTarget = "Hinzufügen";

    public const string UiAddTargetTip = "Ein Programm, die Lautsprecher oder das Mikrofon hinzufügen";

    public const string UiAddProgramItem = "Programm …";

    public const string UiAddSpeakersItem = "Lautsprecher (Standard-Wiedergabegerät)";

    public const string UiAddMicrophoneItem = "Mikrofon (Standard-Aufnahmegerät)";

    public const string UiChooseProgram = "Programm auswählen";

    public const string UiChooseProgramHint = "Programme, die gerade Ton nutzen:";

    public const string UiNoProgramsPlaying = "Gerade nutzt kein Programm Ton. Mit „Datei öffnen …“ geht jedes Programm.";

    public const string UiOpenProgramFile = "Datei öffnen …";

    public const string UiOpenProgramFileTip = "Programmdatei (.exe) wählen, auch wenn das Programm gerade nicht läuft";

    public const string UiOpenProgramFileFilter = "Programme (*.exe)|*.exe";

    public const string UiProgramFileNotExe = "Bitte eine Programmdatei (.exe) wählen.";

    public const string UiAddProgram = "Programm hinzufügen";

    public const string UiProgramPlaceholder = "Oder Namen eintippen, z. B. Discord.exe";

    public const string UiProgramTip = "Name der Programmdatei, auch für Programme, die gerade nicht laufen";

    public const string UiRefreshLists = "Liste aktualisieren";

    public const string UiMoveUp = "Nach oben (das erste Ziel zeigt die Anzeige)";

    public const string UiMoveDown = "Nach unten";

    public const string UiRemoveTarget = "Ziel entfernen";

    public const string UiMuteModeLabel = "Verhalten der Taste";

    public const string UiInvert = "Umkehren (oben = leise)";

    public const string UiMinVolume = "Kleinste Lautstärke";

    public const string UiMaxVolume = "Größte Lautstärke";

    public const string UiMediaCommandLabel = "Medienbefehl";

    public const string UiHotkeyPlaceholder = "z. B. Strg+Alt+M";

    public const string UiHotkeyTip = "Wirkt auf das Programm im Vordergrund, z. B. Strg+Alt+M, Win+D";

    public const string UiSwitchFlowLabel = "Gerätetyp";

    public const string UiSwitchDevicesLabel = "Geräte im Wechsel";

    public const string UiMappingNamePlaceholder = "Eigener Name (optional)";

    public const string UiMappingNameTip = "Titel für Bild und Anzeige, z. B. Spiele";

    public const string UiDropKeptActions = "Weitere Aktionen entfernen";

    public const string UiApply = "Übernehmen";

    public const string UiApplyTip = "Änderungen speichern (Enter)";

    public const string UiDiscard = "Verwerfen";

    public const string UiDiscardTip = "Änderungen verwerfen";

    public const string UiRemoveMapping = "Belegung entfernen";

    // Updates (in the program, from the GitHub releases)
    public const string UiUpdates = "Updates";

    public const string UiCheckForUpdates = "Nach Updates suchen";

    public const string UiCheckForUpdatesOnStart = "Beim Start nach Updates suchen";

    public const string UiInstallUpdate = "Jetzt aktualisieren";

    public const string UiInstallUpdateTip = "Neue Version herunterladen, prüfen und installieren; danach startet Ear Saxophone neu. Belegungen und Einstellungen bleiben erhalten.";

    public const string UiDismissUpdate = "Später";

    public static string UpdateInstalledVersion(string version) => $"Installiert: Version {version}";

    public const string UpdateChecking = "Suche nach Updates …";

    public static string UpdateAvailable(string version) => $"Version {version} ist verfügbar.";

    public static string UpdateUpToDate(string version) => $"Ear Saxophone ist aktuell (Version {version}).";

    public const string UpdateNotReachable =
        "Update-Prüfung nicht möglich – das Repository ist nicht öffentlich oder es besteht keine Verbindung.";

    public const string UpdateRateLimited =
        "Update-Prüfung gerade nicht möglich: GitHub begrenzt die Anfragen ohne Anmeldung. Bitte später erneut versuchen.";

    public const string UpdateUnexpectedAnswer = "Update-Prüfung fehlgeschlagen: GitHub hat unerwartet geantwortet. Bitte später erneut versuchen.";

    public const string UpdateNotAvailableHere = "Updates sind für diese Programmversion nicht verfügbar (unbekannte Versionsnummer).";

    public static string UpdateDownloading(string version, int percent) => $"Version {version} wird heruntergeladen … {percent} %";

    public const string UpdateInstalling = "Das Update wird installiert …";

    public const string UpdateRestarting = "Update installiert. Ear Saxophone startet neu …";

    public static string UpdateDone(string version) => $"Update installiert: Version {version}.";

    public const string UpdateNoChecksumNote =
        "Hinweis: Zu dieser Version gab es keine Prüfsumme (.sha256), der Download wurde ohne Prüfung installiert.";

    public static string UpdateDownloadFailed(string reason) => $"Der Download ist fehlgeschlagen ({reason}). Es wurde nichts verändert.";

    public const string UpdateChecksumMismatch =
        "Die Prüfsumme der heruntergeladenen Datei stimmt nicht (Download beschädigt?). Es wurde nichts verändert.";

    public const string UpdateInvalidPackage =
        "Die heruntergeladene Datei ist kein gültiges Ear-Saxophone-Paket (keine EarSaxophone.exe gefunden). Es wurde nichts verändert.";

    public const string UpdateNoWriteAccess =
        "Kein Schreibzugriff auf den Programmordner, das Update wurde rückgängig gemacht. Verschieben Sie das Programm in einen Ordner, " +
        "in den Sie schreiben dürfen (zum Beispiel C:\\Tools\\EarSaxophone).";

    public static string UpdateInstallFailed(string reason) =>
        $"Das Update konnte nicht installiert werden ({reason}). Alle Programmdateien wurden wiederhergestellt.";

    public static string UpdateRollbackIncomplete(string reason) =>
        $"Das Update konnte nicht installiert werden ({reason}) und nicht alle Dateien ließen sich wiederherstellen. " +
        "Bitte die ZIP-Datei von der Release-Seite herunterladen und in den Programmordner entpacken.";

    public static string UpdateRestartFailed(string reason) =>
        $"Das Update ist installiert, aber der Neustart ist fehlgeschlagen ({reason}). Bitte Ear Saxophone selbst neu starten.";

    // Mini picture of the question for a new program
    public static string PromptMiniQuestion(string program) => $"{program} spielt Ton. Auf welchen Regler legen?";

    // LEDs am Gerät (Statuszeile, nie als Fehler)
    public const string UiLedFeedback = "LEDs am Gerät anzeigen";

    public const string UiLedFeedbackTip =
        "Eine Taste mit Stumm-Aktion leuchtet, solange ihr Ziel stumm ist. Dafür muss am nanoKONTROL2 im Korg KONTROL Editor der LED-Modus „External“ eingestellt sein.";

    public static string LedOutputOpened(string port) => $"LED-Anzeige über den MIDI-Ausgang {port} aktiv.";

    public static string LedOutputNotFound(string fragment) =>
        $"LED-Anzeige: kein MIDI-Ausgang mit „{fragment}“ im Namen gefunden. Die Tasten funktionieren trotzdem.";

    public static string LedOutputOpenFailed(string port, string reason) =>
        $"LED-Anzeige: der MIDI-Ausgang {port} ließ sich nicht öffnen ({reason}). Die Tasten funktionieren trotzdem.";

    public static string LedOutputListFailed(string reason) =>
        $"LED-Anzeige: MIDI-Ausgänge konnten nicht aufgelistet werden ({reason}). Die Tasten funktionieren trotzdem.";

    public static string LedSendFailed(string port, string reason) =>
        $"LED-Anzeige: Senden an {port} fehlgeschlagen ({reason}). Es wird erneut versucht.";

    // Question for a new program with sound: header band and hint at the countdown bar
    public const string PromptHeader = "Neues Programm mit Ton";

    public const string PromptAutoCloseHint = "Schließt sich von selbst, wenn du nichts wählst.";

    public const string UiPromptSound = "Ton beim Nachfragen";

    // Streifen-Ziel "Wie Fader": Tasten und Drehregler eines Streifens nehmen die Ziele seines Faders
    public static string UiStripTargetName(string fader, string targets) => $"Wie {fader} ({targets})";

    public const string UiStripTargetNone = "noch ohne Ziel";

    public const string UiAddStripItem = "Wie der Fader dieses Streifens";

    public const string UiAddStripItemTip =
        "Nimmt immer die Programme des Faders in derselben Spalte. Kommt am Fader ein Programm dazu, gilt es hier sofort mit.";

    public const string UiStripTargetNotHere = "„Wie Fader“ geht nur bei Tasten und Drehreglern eines Streifens, nicht beim Fader selbst.";

    public const string TargetStripEmptyShort = "wie Fader (ohne Ziel)";

    public static string StripTargetEmpty(string fader) =>
        $"{fader} steuert keine Lautstärke-Ziele, deshalb hat „Wie {fader}“ nichts zu schalten. Lege am Fader Programme fest oder wähle hier eigene Ziele.";

    public const string StripTargetNoFader = "Dieses Element gehört zu keinem Streifen mit Fader, „Wie Fader“ hat hier kein Ziel.";

    public static string StripTargetMisplaced(ControlLabel control) =>
        $"{control}: „Wie Fader“ passt nur zu Tasten und Drehreglern eines Streifens (nicht zum Fader selbst) und bleibt hier ohne Wirkung.";

    public const string StripTargetUnresolved = "„Wie Fader“ konnte nicht aufgelöst werden, es wurde nichts geändert.";

    // LEDs blinken bei Stumm
    public const string UiLedBlink = "LEDs blinken bei Stumm";

    public const string UiLedBlinkTip =
        "Stumm geschaltete Tasten blinken langsam (am Gerät und im Bild), statt dauerhaft zu leuchten. Aus: Dauerlicht.";
}

/// <summary>Display text of a control, used inside messages.</summary>
public readonly record struct ControlLabel(string Text)
{
    public override string ToString() => Text;
}
