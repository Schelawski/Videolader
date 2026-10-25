using System.Globalization;

namespace Videolader.Core;

/// <summary>Languages of the user interface and the command line.</summary>
public enum UiLanguage
{
    English,
    German,
    Russian,
}

/// <summary>Codes, names and selection of the interface language.</summary>
public static class UiLanguages
{
    /// <summary>In the order shown in the language selection.</summary>
    public static IReadOnlyList<UiLanguage> All { get; } = [UiLanguage.English, UiLanguage.German, UiLanguage.Russian];

    /// <summary>Code stored in the settings file and accepted by <c>--lang</c>.</summary>
    public static string Code(UiLanguage language) => language switch
    {
        UiLanguage.German => "de",
        UiLanguage.Russian => "ru",
        _ => "en",
    };

    /// <summary>Name of the language in that language, as shown in the language selection.</summary>
    public static string NativeName(UiLanguage language) => language switch
    {
        UiLanguage.German => "Deutsch",
        UiLanguage.Russian => "Русский",
        _ => "English",
    };

    /// <summary>Parses a stored code; <c>null</c> if empty or unknown.</summary>
    public static UiLanguage? FromCode(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "en" => UiLanguage.English,
        "de" => UiLanguage.German,
        "ru" => UiLanguage.Russian,
        _ => null,
    };

    /// <summary>German or Russian for a German or Russian Windows display language, English otherwise.</summary>
    public static UiLanguage FromCulture(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "de" => UiLanguage.German,
        "ru" => UiLanguage.Russian,
        _ => UiLanguage.English,
    };

    /// <summary>The language chosen in the settings, or the one matching Windows if none was chosen yet.</summary>
    public static UiLanguage Resolve(string? storedCode, CultureInfo windowsCulture) =>
        FromCode(storedCode) ?? FromCulture(windowsCulture);

    /// <summary>
    /// Switches all texts, and the culture of .NET's own messages (e.g. file system errors), to <paramref name="language"/>.
    /// Number and date formats keep following Windows.
    /// </summary>
    public static void Apply(UiLanguage language)
    {
        UiText.Language = language;

        var culture = CultureInfo.GetCultureInfo(language switch
        {
            UiLanguage.German => "de-DE",
            UiLanguage.Russian => "ru-RU",
            _ => "en-US",
        });
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}

/// <summary>
/// All user-visible texts of Videolader (window, command line, messages), in English, German and Russian.
/// </summary>
/// <remarks>
/// Every text is written once with all translations side by side: <c>L("English", "German", "Russian")</c>.
/// A missing translation is therefore a compile error, and the versions can be reviewed together.
/// The language is set once at start (see <see cref="UiLanguages.Apply"/>). Output of yt-dlp stays as it is.
/// </remarks>
public static class UiText
{
    /// <summary>Language of all texts.</summary>
    public static UiLanguage Language { get; set; } = UiLanguage.English;

    private static string L(string english, string german, string russian) => Language switch
    {
        UiLanguage.German => german,
        UiLanguage.Russian => russian,
        _ => english,
    };

    // ----- Tabs, groups, buttons -----

    public static string TabDownload => L("Download", "Download", "Загрузка");
    public static string TabSettings => L("Settings", "Einstellungen", "Настройки");
    public static string TabSettingsToolsMissing => L("Settings – tools missing", "Einstellungen – Werkzeuge fehlen", "Настройки – нет инструментов");
    public static string GroupInput => L("Videos and playlists", "Videos und Playlists", "Видео и плейлисты");
    public static string GroupOptions => L("Options", "Optionen", "Параметры");
    public static string GroupLog => L("Log", "Protokoll", "Журнал");
    public static string LoadList => L("Load list", "Liste laden", "Загрузить список");
    public static string Clear => L("Clear", "Leeren", "Очистить");
    public static string CtrlEnterHint => L("(Ctrl+Enter)", "(Strg+Enter)", "(Ctrl+Enter)");
    public static string Browse => L("Browse…", "Durchsuchen…", "Обзор…");
    public static string Open => L("Open", "Öffnen", "Открыть");
    public static string SelectAll => L("All", "Alle", "Все");
    public static string SelectNone => L("None", "Keine", "Ничего");
    public static string SelectOnlyNew => L("Only new", "Nur neue", "Только новые");
    public static string HideLog => L("Hide log", "Protokoll ausblenden", "Скрыть журнал");
    public static string ShowLog => L("Show log", "Protokoll anzeigen", "Показать журнал");
    public static string Download => L("Download", "Herunterladen", "Скачать");
    public static string Cancel => L("Cancel", "Abbrechen", "Отмена");
    public static string ToolsSetUp => L("Set up…", "Einrichten…", "Установить…");
    public static string ToolsUpdate => L("Update", "Aktualisieren", "Обновить");

    // ----- Input and options -----

    public static string InputTip => L(
        "One entry per line: links or IDs of videos, playlists or channels.",
        "Ein Eintrag pro Zeile: Video-, Playlist- oder Kanal-Links bzw. IDs.",
        "По одной записи на строку: ссылки или ID видео, плейлистов или каналов.");

    public static string OutputFolderLabel => L("Folder:", "Zielordner:", "Папка:");
    public static string QualityLabel => L("Quality:", "Qualität:", "Качество:");
    public static string SkipExisting => L("Skip existing", "Vorhandene überspringen", "Пропускать имеющиеся");
    public static string SubtitlesLabel => L("Subtitles:", "Untertitel:", "Субтитры:");
    public static string DownloadSubtitles => L("download", "herunterladen", "скачивать");
    public static string LanguagesLabel => L("Languages:", "Sprachen:", "Языки:");
    public static string PauseLabel => L("Pause:", "Pause:", "Пауза:");
    public static string SecondsUnit => L("s", "s", "с");
    public static string RandomPause => L("random up to +50%", "zufällig bis +50 %", "случайно до +50 %");
    public static string LimitLabel => L("Limit:", "Limit:", "Лимит:");
    public static string VideosPerRun => L("videos per run", "Videos pro Lauf", "видео за запуск");
    public static string LimitHint => L("(0 = all checked)", "(0 = alle markierten)", "(0 = все)");

    public static string TipSkipExisting => L(
        "Skip videos that are already in the folder or listed in the archive.",
        "Videos überspringen, die schon im Zielordner liegen oder im Archiv stehen.",
        "Пропускать видео, которые уже лежат в папке или записаны в архиве.");

    public static string TipLanguages => L(
        "e.g. ru,de,en – the first language is also used for the titles in the list.",
        "z. B. ru,de,en – die erste Sprache gilt auch für die Titel in der Liste.",
        "например ru,de,en – первый язык используется и для названий в списке.");

    public static string TipPause => L(
        "Minimum pause between two downloads, protects against YouTube's throttling.",
        "Mindestpause zwischen zwei Downloads, schützt vor YouTubes Drosselung.",
        "Минимальная пауза между двумя загрузками, защищает от ограничений YouTube.");

    public static string TipRandomPause => L(
        "Each pause is lengthened by a random amount of up to 50%, so there is no regular rhythm.",
        "Jede Pause wird zufällig um bis zu 50 % verlängert, damit kein gleichmäßiger Takt entsteht.",
        "Каждая пауза случайно удлиняется до 50 %, чтобы не было равномерного ритма.");

    // ----- Help -----

    public static string HelpButton => L("Help", "Hilfe", "Справка");
    public static string HelpTitle => L("Videolader – Help", "Videolader – Hilfe", "Videolader – справка");
    public static string HelpButtonTooltip => L("Open the help (F1)", "Hilfe öffnen (F1)", "Открыть справку (F1)");
    public static string HelpClickForMore => L("Click the “?” to learn more.", "Klicke auf das „?“ für mehr.", "Нажмите «?», чтобы узнать больше.");

    /// <summary>Added to a tip in the log: where the help explains it.</summary>
    public static string SeeHelp(string topicTitle) => L($"→ Help: “{topicTitle}” (F1)", $"→ Hilfe: „{topicTitle}“ (F1)", $"→ Справка: «{topicTitle}» (F1)");

    public static string HelpTipQuality => L(
        "Up to 1080p is the default. “Audio only” saves just the sound. Subtitles are saved as Whisper JSON and SRT.",
        "Bis 1080p ist der Standard. „Nur Audio“ speichert nur den Ton. Untertitel werden als Whisper-JSON und SRT gespeichert.",
        "По умолчанию до 1080p. «Только аудио» сохраняет только звук. Субтитры сохраняются как Whisper JSON и SRT.");

    public static string HelpTipExisting => L(
        "Videos in the folder or in videolader-archiv.txt are never downloaded twice.",
        "Videos im Zielordner oder in videolader-archiv.txt werden nie doppelt geladen.",
        "Видео из папки или из videolader-archiv.txt никогда не скачиваются повторно.");

    public static string HelpTipPause => L(
        "From experience: with 30 s YouTube often blocks, from 45 s even 100 videos in a row usually work.",
        "Erfahrung: Mit 30 s sperrt YouTube oft, ab 45 s laufen meist sogar 100 Videos am Stück durch.",
        "Из опыта: при 30 с YouTube часто блокирует, от 45 с обычно проходят даже 100 видео подряд.");

    public static string HelpTipCookies => L(
        "Usually not needed. If YouTube asks you to sign in: use Firefox – Chrome, Edge and Brave lock their cookies.",
        "Meist nicht nötig. Verlangt YouTube eine Anmeldung: Firefox nehmen – Chrome, Edge und Brave sperren ihre Cookies.",
        "Обычно не нужны. Если YouTube требует вход: используйте Firefox – Chrome, Edge и Brave блокируют свои cookies.");

    public static string HelpTipApiKey => L(
        "Optional. How to create a free key and why downloading works without it.",
        "Freiwillig. So legst du einen kostenlosen Key an, und warum das Herunterladen auch ohne geht.",
        "Необязательно. Как получить бесплатный ключ и почему скачивание работает и без него.");

    public static string HelpTipTools => L(
        "yt-dlp, ffmpeg and Deno do the actual work. yt-dlp must stay up to date.",
        "yt-dlp, ffmpeg und Deno erledigen die eigentliche Arbeit. yt-dlp muss aktuell bleiben.",
        "yt-dlp, ffmpeg и Deno выполняют основную работу. yt-dlp нужно обновлять.");

    // ----- Settings tab -----

    public static string UiLanguageLabel => L("Language:", "Sprache:", "Язык:");

    public static string UiLanguageHint => L(
        "The window reopens in the chosen language.",
        "Das Fenster öffnet sich in der gewählten Sprache neu.",
        "Окно откроется заново на выбранном языке.");

    public static string CookiesLabel => L("Cookies from browser:", "Cookies aus Browser:", "Cookies из браузера:");
    public static string CookiesNone => L("none", "keine", "нет");

    public static string CookiesHint => L(
        "Only needed if YouTube asks you to sign in (“Sign in to confirm you're not a bot”). " +
        "Firefox works; Chrome, Edge and Brave usually lock their cookies on Windows.",
        "Nur nötig, wenn YouTube eine Anmeldung verlangt („Sign in to confirm you're not a bot“). " +
        "Firefox funktioniert; Chrome, Edge und Brave sperren ihre Cookies unter Windows meist.",
        "Нужны, только если YouTube требует вход («Sign in to confirm you're not a bot»). " +
        "Firefox работает; Chrome, Edge и Brave в Windows обычно блокируют свои cookies.");

    public static string BrowserUsuallyLocked(string browser) =>
        browser + L(" (usually locked)", " (meist gesperrt)", " (обычно заблокирован)");

    public static string ApiKeyLabel => L("YouTube API key:", "YouTube-API-Key:", "Ключ YouTube API:");
    public static string ShowKey => L("show", "anzeigen", "показать");

    public static string ApiKeyHint => L(
        "Optional – loads playlists and titles faster, not needed for downloading.",
        "Optional – lädt Playlists und Titel schneller, für den Download nicht nötig.",
        "Необязательно – быстрее загружает плейлисты и названия, для скачивания не нужен.");

    public static string ToolsLabel => L("Tools:", "Werkzeuge:", "Инструменты:");
    public static string ToolMissing => L("✗ missing", "✗ fehlt", "✗ нет");

    public static string ToolsHint(string folder) => L(
        $"yt-dlp, ffmpeg and Deno are in {folder}. yt-dlp is updated automatically once a day.",
        $"yt-dlp, ffmpeg und Deno liegen in {folder}. yt-dlp wird einmal am Tag automatisch aktualisiert.",
        $"yt-dlp, ffmpeg и Deno находятся в {folder}. yt-dlp обновляется автоматически раз в день.");

    // ----- List -----

    public static string ColumnNumber => L("No.", "Nr.", "№");
    public static string ColumnTitle => L("Title", "Titel", "Название");
    public static string ColumnDuration => L("Duration", "Dauer", "Длит.");
    public static string ColumnPlaylist => L("Playlist", "Playlist", "Плейлист");
    public static string ColumnStatus => L("Status", "Status", "Статус");
    public static string MenuOpenInBrowser => L("Open in browser", "Im Browser öffnen", "Открыть в браузере");
    public static string MenuCopyIds => L("Copy IDs", "IDs kopieren", "Копировать ID");
    public static string MenuCopyLinks => L("Copy links", "Links kopieren", "Копировать ссылки");
    public static string MenuCheckSelected => L("Check selected", "Markierte auswählen", "Отметить выделенные");
    public static string MenuUncheckSelected => L("Uncheck selected", "Markierte abwählen", "Снять отметку с выделенных");
    public static string NoListLoaded => L("No list loaded yet.", "Noch keine Liste geladen.", "Список ещё не загружен.");

    public static string ListSummary(int total, int selected, int existing) => L(
        $"{Videos(total)} · {selected} selected · {existing} existing",
        $"{Videos(total)} · {selected} ausgewählt · {existing} vorhanden",
        $"{Videos(total)} · выбрано: {selected} · имеется: {existing}");

    public static string TitleUnknown => L("(title unknown)", "(Titel unbekannt)", "(название неизвестно)");

    // ----- Row status -----

    public static string StatusNew => L("New", "Neu", "Новое");
    public static string StatusExisting => L("Already there", "Bereits vorhanden", "Уже есть");
    public static string StatusUnavailable => L("Not available", "Nicht verfügbar", "Недоступно");
    public static string StatusWaiting => L("Waiting", "Wartet", "Ожидает");
    public static string StatusSkipped => L("Skipped (exists)", "Übersprungen (vorhanden)", "Пропущено (уже есть)");
    public static string StatusStarting => L("Starting…", "Startet…", "Запуск…");
    public static string StatusCanceled => L("Cancelled", "Abgebrochen", "Отменено");

    public static string StatusDone(IReadOnlyCollection<string> subtitleLanguages) =>
        L("Done", "Fertig", "Готово") + SubtitlesSuffix(subtitleLanguages);

    public static string StatusFailed(string? error) => L("Error: ", "Fehler: ", "Ошибка: ") + error;

    /// <summary>" + subtitles ru, de", or empty without subtitles.</summary>
    public static string SubtitlesSuffix(IReadOnlyCollection<string> languages) => languages.Count == 0
        ? string.Empty
        : L(" + subtitles ", " + Untertitel ", " + субтитры ") + string.Join(", ", languages);

    // ----- Download phases (status column) -----

    public static string PhaseDownloading => L("Downloading", "Lädt", "Загрузка");
    public static string PhaseDownloadingPart(int part) => L($"Downloading part {part}", $"Lädt Teil {part}", $"Загрузка, часть {part}");
    public static string PhaseMerging => L("Merging…", "Zusammenführen…", "Объединение…");
    public static string PhaseExtractingAudio => L("Converting audio…", "Audio umwandeln…", "Преобразование аудио…");
    public static string PhaseSubtitles => L("Subtitles…", "Untertitel…", "Субтитры…");
    public static string PhaseWritingFiles => L("Writing files…", "Dateien schreiben…", "Запись файлов…");

    // ----- Busy labels, log and dialogs of the window -----

    public static string Cancelling => L("Cancelling…", "Wird abgebrochen…", "Отмена…");
    public static string Cancelled => L("Cancelled.", "Abgebrochen.", "Отменено.");
    public static string ToolsFolderLog(string folder) => L($"Tools folder: {folder}", $"Werkzeug-Ordner: {folder}", $"Папка инструментов: {folder}");

    public static string ToolsMissingLog(string missing) => L(
        $"Missing: {missing}. Under “Settings”, click “Set up…” next to “Tools” – they are downloaded automatically.",
        $"Es fehlen: {missing}. Klicke unter „Einstellungen“ bei „Werkzeuge“ auf „Einrichten…“ – sie werden automatisch heruntergeladen.",
        $"Не хватает: {missing}. На вкладке «Настройки» нажмите «Установить…» рядом с пунктом «Инструменты» – они скачаются автоматически.");

    public static string UpdatingYtDlp => L("Updating yt-dlp…", "yt-dlp wird aktualisiert…", "Обновление yt-dlp…");

    public static string ConfirmCloseWhileBusy => L(
        "An operation is still running. Cancel it and quit?",
        "Es läuft noch ein Vorgang. Abbrechen und beenden?",
        "Ещё выполняется операция. Прервать её и выйти?");

    public static string NotRecognized(string input) => L($"Not recognized: {input}", $"Nicht erkannt: {input}", $"Не распознано: {input}");

    public static string EnterAtLeastOneLink => L(
        "Please enter at least one YouTube link or video/playlist ID.",
        "Bitte mindestens einen YouTube-Link oder eine Video-/Playlist-ID eingeben.",
        "Введите хотя бы одну ссылку YouTube или ID видео/плейлиста.");

    public static string LoadingList => L("Loading list…", "Liste wird geladen…", "Загрузка списка…");

    public static string LoadingListLog(int videos, int playlists) => L(
        $"Loading list: {Count(videos, "single video", "single videos")}, {Count(playlists, "playlist/channel", "playlists/channels")}…",
        $"Lade Liste: {Count(videos, "Einzelvideo", "Einzelvideos")}, {Count(playlists, "Playlist/Kanal", "Playlists/Kanäle")}…",
        $"Загрузка списка: отдельных видео – {videos}, плейлистов/каналов – {playlists}…");

    public static string ListLoadedLog(int total, int existing, int unavailable) => L(
        $"{Videos(total)} in the list, {existing} of them already there" + (unavailable > 0 ? $" and {unavailable} not available." : "."),
        $"{Videos(total)} in der Liste, davon {existing} bereits vorhanden" + (unavailable > 0 ? $" und {unavailable} nicht verfügbar." : "."),
        $"В списке {Videos(total)}, из них уже есть: {existing}" + (unavailable > 0 ? $", недоступно: {unavailable}." : "."));

    public static string NeedYtDlpAndFfmpeg => L(
        "yt-dlp and ffmpeg are needed. Under “Settings”, click “Set up…” next to “Tools”.",
        "yt-dlp und ffmpeg werden benötigt. Klicke unter „Einstellungen“ bei „Werkzeuge“ auf „Einrichten…“.",
        "Нужны yt-dlp и ffmpeg. На вкладке «Настройки» нажмите «Установить…» рядом с пунктом «Инструменты».");

    public static string DenoMissingWarning => L(
        "Warning: Deno is missing – YouTube then often offers only limited formats.",
        "Warnung: Deno fehlt – YouTube liefert dann oft nur eingeschränkte Formate.",
        "Внимание: нет Deno – тогда YouTube часто отдаёт только ограниченные форматы.");

    public static string NoVideosSelected => L(
        "No videos selected. Load a list first and check the videos.",
        "Keine Videos ausgewählt. Lade zuerst eine Liste und setze Häkchen.",
        "Видео не выбраны. Сначала загрузите список и отметьте видео.");

    public static string DownloadRunning => L("Downloading…", "Download läuft…", "Идёт загрузка…");
    public static string PauseProgress(TimeSpan pause) => L($"Pause {pause.TotalSeconds:0} s…", $"Pause {pause.TotalSeconds:0} s…", $"Пауза {pause.TotalSeconds:0} с…");
    public static string ProgressOf(int number, int total, string title) => L($"{number} of {total}: {title}", $"{number} von {total}: {title}", $"{number} из {total}: {title}");
    public static string SavedLog(string file) => L($"✔ {file} saved.", $"✔ {file} gespeichert.", $"✔ {file} сохранён.");
    public static string Tip(string hint) => L("Tip: ", "Tipp: ", "Совет: ") + hint;
    public static string Error(string message) => L("Error: ", "Fehler: ", "Ошибка: ") + message;

    public static string FinishedLog(int done, int failed, int skipped) => L(
        $"Finished: {done} downloaded, {failed} failed, {skipped} skipped.",
        $"Fertig: {done} heruntergeladen, {failed} Fehler, {skipped} übersprungen.",
        $"Готово: скачано {done}, ошибок {failed}, пропущено {skipped}.");

    public static string LimitReachedLog(int limit, int remaining) => L(
        $"Limit of {Videos(limit)} reached. {remaining} still checked – click “Download” again to continue.",
        $"Limit von {Videos(limit)} erreicht. Noch {remaining} markiert – zum Weitermachen erneut auf „Herunterladen“ klicken.",
        $"Достигнут лимит в {Videos(limit)}. Ещё отмечено: {remaining} – чтобы продолжить, снова нажмите «Скачать».");

    public static string CookieStopLog => L(
        "Download stopped: the cookies cannot be read from the browser.",
        "Download gestoppt: Die Cookies lassen sich nicht aus dem Browser lesen.",
        "Загрузка остановлена: cookies не удаётся прочитать из браузера.");

    public static string CookieStopQuestion(string browser) => L(
        $"The cookies from “{browser}” cannot be read, so the download was stopped.\n\n" +
        "Chrome, Edge and Brave lock their cookies on Windows while the browser is running and encrypt them so " +
        "that yt-dlp cannot read them. Cookies are only needed if YouTube asks you to sign in.\n\n" +
        "Turn cookies off and then simply click “Download” again?",
        $"Die Cookies aus „{browser}“ lassen sich nicht lesen, deshalb wurde der Download gestoppt.\n\n" +
        "Chrome, Edge und Brave sperren ihre Cookies unter Windows, solange der Browser läuft, und verschlüsseln " +
        "sie so, dass yt-dlp sie nicht lesen kann. Cookies sind nur nötig, wenn YouTube eine Anmeldung verlangt.\n\n" +
        "Cookies ausschalten und danach einfach erneut auf „Herunterladen“ klicken?",
        $"Cookies из «{browser}» не удаётся прочитать, поэтому загрузка остановлена.\n\n" +
        "Chrome, Edge и Brave в Windows блокируют свои cookies, пока браузер запущен, и шифруют их так, " +
        "что yt-dlp не может их прочитать. Cookies нужны, только если YouTube требует вход.\n\n" +
        "Отключить cookies и затем просто снова нажать «Скачать»?");

    public static string CookiesTurnedOff => L("Cookies turned off.", "Cookies ausgeschaltet.", "Cookies отключены.");

    public static string ToolsDownloadQuestion(string missing, string folder, bool withFfmpeg) => L(
        $"The following tools will be downloaded from GitHub: {missing}.\n\nFolder: {folder}\n" +
        (withFfmpeg ? "ffmpeg is about 190 MB.\n" : string.Empty) + "\nContinue?",
        $"Folgende Werkzeuge werden von GitHub heruntergeladen: {missing}.\n\nZielordner: {folder}\n" +
        (withFfmpeg ? "ffmpeg ist ca. 190 MB groß.\n" : string.Empty) + "\nFortfahren?",
        $"С GitHub будут скачаны инструменты: {missing}.\n\nПапка: {folder}\n" +
        (withFfmpeg ? "ffmpeg занимает около 190 МБ.\n" : string.Empty) + "\nПродолжить?");

    public static string ToolsBusy => L("Tools…", "Werkzeuge…", "Инструменты…");
    public static string DownloadingTool(string tool) => L($"Downloading {tool}…", $"{tool} wird heruntergeladen…", $"Скачивается {tool}…");

    public static string ToolProgress(string tool, long received, long? total) =>
        $"{tool}: {Megabytes(received)}" + (total is > 0 ? L(" of ", " von ", " из ") + Megabytes(total.Value) : string.Empty);

    public static string AllToolsReady => L("All tools are set up.", "Alle Werkzeuge sind eingerichtet.", "Все инструменты установлены.");
    public static string ToolsStillMissing => L("Some tools are still missing.", "Es fehlen noch Werkzeuge.", "Ещё не хватает инструментов.");
    public static string CheckingForUpdates(string tool) => L($"Checking for {tool} updates…", $"Prüfe auf {tool}-Updates…", $"Проверка обновлений {tool}…");
    public static string YtDlpVersion(string version) => L($"yt-dlp version {version}", $"yt-dlp Version {version}", $"yt-dlp версии {version}");
    public static string YtDlpCannotStart(string message) => L("yt-dlp cannot be started: ", "yt-dlp lässt sich nicht starten: ", "Не удаётся запустить yt-dlp: ") + message;
    public static string SettingsNotSaved(string message) => L("Settings could not be saved: ", "Einstellungen konnten nicht gespeichert werden: ", "Не удалось сохранить настройки: ") + message;
    public static string ChooseFolderTitle => L("Choose the folder for the videos", "Ordner für die Videos wählen", "Выберите папку для видео");
    public static string FolderCannotBeCreated(string message) => L("The folder cannot be created:\n", "Der Zielordner kann nicht angelegt werden:\n", "Не удаётся создать папку:\n") + message;
    public static string UnexpectedError(string message) => L("Unexpected error:\n\n", "Unerwarteter Fehler:\n\n", "Непредвиденная ошибка:\n\n") + message;

    // ----- Messages of the download, list and tools -----

    public static string YtDlpExitedWithoutVideo(int exitCode) => L(
        $"yt-dlp exited with code {exitCode}, no video file found.",
        $"yt-dlp beendet mit Code {exitCode}, keine Videodatei gefunden.",
        $"yt-dlp завершился с кодом {exitCode}, видеофайл не найден.");

    public static string YtDlpExited(int exitCode) => L($"yt-dlp exited with code {exitCode}.", $"yt-dlp beendet mit Code {exitCode}.", $"yt-dlp завершился с кодом {exitCode}.");
    public static string YtDlpNotFound => L("yt-dlp was not found.", "yt-dlp wurde nicht gefunden.", "yt-dlp не найден.");
    public static string Note(string message) => L("Note: ", "Hinweis: ", "Примечание: ") + message;
    public static string SubtitleEmpty(string language) => L($"Subtitles {language}: empty, skipped.", $"Untertitel {language}: leer, übersprungen.", $"Субтитры {language}: пустые, пропущены.");

    public static string SubtitleSaved(string language, bool automatic, int segments, string file) => L(
        $"Subtitles {language} ({(automatic ? "automatic" : "uploaded")}): {segments} segments → {file}",
        $"Untertitel {language} ({(automatic ? "automatisch" : "hochgeladen")}): {segments} Abschnitte → {file}",
        $"Субтитры {language} ({(automatic ? "автоматические" : "загруженные")}): фрагментов: {segments} → {file}");

    public static string SubtitleNotConverted(string language, string message) => L(
        $"Subtitles {language} could not be converted: {message}",
        $"Untertitel {language} konnten nicht umgewandelt werden: {message}",
        $"Не удалось преобразовать субтитры {language}: {message}");

    public static string NoSubtitlesFound => L(
        "No subtitles found in the chosen languages.",
        "Keine Untertitel in den gewählten Sprachen gefunden.",
        "Субтитры на выбранных языках не найдены.");

    public static string SubtitleFormatNotSupported(string extension) => L(
        $"Subtitle format {extension} is not supported.",
        $"Untertitelformat {extension} wird nicht unterstützt.",
        $"Формат субтитров {extension} не поддерживается.");

    public static string PlaylistLoaded(string name, int count, int alreadyInList) => L(
        $"“{name}”: {Videos(count)}" + (alreadyInList > 0 ? $", {alreadyInList} of them already in the list" : string.Empty) + ".",
        $"„{name}“: {Videos(count)}" + (alreadyInList > 0 ? $", davon {alreadyInList} schon in der Liste" : string.Empty) + ".",
        $"«{name}»: {Videos(count)}" + (alreadyInList > 0 ? $", из них уже в списке: {alreadyInList}" : string.Empty) + ".");

    public static string DuplicatesMerged(int count) => L(
        $"{count} duplicate {(count == 1 ? "entry was" : "entries were")} merged.",
        $"{count} doppelte {(count == 1 ? "Eintrag wurde" : "Einträge wurden")} zusammengefasst.",
        $"Объединено повторяющихся записей: {count}.");

    public static string ApiFallbackToYtDlp(string message) => L(
        $"YouTube API: {message} – trying yt-dlp.",
        $"YouTube-API: {message} – versuche es mit yt-dlp.",
        $"YouTube API: {message} – пробую через yt-dlp.");

    public static string ApiFallbackForTitles(string message) => L(
        $"YouTube API: {message} – titles are read without the API.",
        $"YouTube-API: {message} – Titel werden ohne API ermittelt.",
        $"YouTube API: {message} – названия получаются без API.");

    public static string PlaylistNeedsYtDlp(string playlist) => L(
        $"Playlist {playlist} cannot be loaded: yt-dlp is missing (or enter an API key).",
        $"Playlist {playlist} kann nicht geladen werden: yt-dlp fehlt (oder API-Key eintragen).",
        $"Плейлист {playlist} не удаётся загрузить: нет yt-dlp (или укажите ключ API).");

    public static string ChecksumMismatch(string tool) => L(
        $"The download of {tool} does not match the checksum published by the project and was deleted. Please try again later.",
        $"Der Download von {tool} passt nicht zur Prüfsumme, die das Projekt veröffentlicht hat, und wurde gelöscht. Bitte später erneut versuchen.",
        $"Загрузка {tool} не совпадает с контрольной суммой, опубликованной проектом, и была удалена. Попробуйте позже ещё раз.");

    public static string ChecksumMissing(string tool) => L(
        $"No published checksum was found for {tool}, so nothing was installed.",
        $"Für {tool} wurde keine veröffentlichte Prüfsumme gefunden, deshalb wurde nichts eingerichtet.",
        $"Для {tool} не найдена опубликованная контрольная сумма, поэтому ничего не установлено.");

    public static string EmptyFile => L("Empty file.", "Leere Datei.", "Пустой файл.");

    public static string FileNotInArchive(string file) => L(
        $"{file} is not in the downloaded archive.",
        $"{file} ist nicht im heruntergeladenen Archiv enthalten.",
        $"{file} отсутствует в скачанном архиве.");

    public static string QualityName(VideoQuality quality) => quality switch
    {
        VideoQuality.Best => L("Best quality (MP4)", "Beste Qualität (MP4)", "Лучшее качество (MP4)"),
        VideoQuality.Max1080 => L("Up to 1080p (MP4)", "Bis 1080p (MP4)", "До 1080p (MP4)"),
        VideoQuality.Max720 => L("Up to 720p (MP4)", "Bis 720p (MP4)", "До 720p (MP4)"),
        VideoQuality.Max480 => L("Up to 480p (MP4)", "Bis 480p (MP4)", "До 480p (MP4)"),
        VideoQuality.AudioOnly => L("Audio only (M4A)", "Nur Audio (M4A)", "Только аудио (M4A)"),
        _ => quality.ToString(),
    };

    // ----- Hints for known yt-dlp errors -----

    public static string HintCookies => L(
        "The cookies cannot be read from the browser. Chrome, Edge and Brave lock them on Windows while the browser " +
        "is running and encrypt them so that yt-dlp cannot read them. Set cookies to “none” (usually not needed at all) or use Firefox.",
        "Die Cookies lassen sich nicht aus dem Browser lesen. Chrome, Edge und Brave sperren sie unter Windows, " +
        "solange der Browser läuft, und verschlüsseln sie so, dass yt-dlp sie nicht lesen kann. " +
        "Stell die Cookies auf „keine“ (meist gar nicht nötig) oder nimm Firefox.",
        "Cookies не удаётся прочитать из браузера. Chrome, Edge и Brave в Windows блокируют их, пока браузер запущен, " +
        "и шифруют так, что yt-dlp не может их прочитать. Выберите для cookies «нет» (обычно они вообще не нужны) или используйте Firefox.");

    public static string HintSignIn => L(
        "YouTube asks you to sign in. Under “Settings”, choose a browser for “Cookies from browser” in which you are " +
        "signed in to YouTube (Firefox works, Chrome/Edge/Brave usually don't).",
        "YouTube verlangt eine Anmeldung. Wähle unter „Einstellungen“ bei „Cookies aus Browser“ einen Browser, in dem du " +
        "bei YouTube angemeldet bist (Firefox funktioniert, Chrome/Edge/Brave meist nicht).",
        "YouTube требует вход. На вкладке «Настройки» в поле «Cookies из браузера» выберите браузер, в котором вы вошли в YouTube " +
        "(Firefox работает, Chrome/Edge/Brave обычно нет).");

    public static string HintThrottled => L(
        "YouTube is throttling the requests (429). Wait a while and increase the pause between downloads.",
        "YouTube drosselt die Anfragen (429). Warte etwas und erhöhe die Pause zwischen den Downloads.",
        "YouTube ограничивает запросы (429). Подождите и увеличьте паузу между загрузками.");

    public static string HintPrivate => L("The video is private.", "Das Video ist privat.", "Это видео приватное.");

    public static string HintDeno => L(
        "Deno is missing or outdated. Set up the tools again (Settings → Tools).",
        "Deno fehlt oder ist veraltet. Richte die Werkzeuge neu ein (Einstellungen → Werkzeuge).",
        "Deno отсутствует или устарел. Установите инструменты заново (Настройки → Инструменты).");

    public static string HintFfmpeg => L(
        "ffmpeg is missing. Set up the tools (Settings → Tools).",
        "ffmpeg fehlt. Richte die Werkzeuge ein (Einstellungen → Werkzeuge).",
        "Нет ffmpeg. Установите инструменты (Настройки → Инструменты).");

    // ----- Command line -----

    public static string CliNoValidLinks => L("No valid links or IDs.", "Keine gültigen Links oder IDs.", "Нет корректных ссылок или ID.");

    public static string CliToolsMissing => L(
        "yt-dlp and ffmpeg are missing. Set them up once with: Videolader.exe setup",
        "yt-dlp und ffmpeg fehlen. Einmalig einrichten mit: Videolader.exe setup",
        "Нет yt-dlp и ffmpeg. Установите их один раз командой: Videolader.exe setup");

    public static string CliDenoMissing => DenoMissingWarning + " (Videolader.exe setup)";
    public static string CliLoadingList => L("Loading list…", "Lade Liste…", "Загрузка списка…");

    public static string CliVideosFound(int count, string folder) => L(
        $"{Videos(count)} found. Folder: {folder}",
        $"{Videos(count)} gefunden. Zielordner: {folder}",
        $"Найдено {Videos(count)}. Папка: {folder}");

    public static string CliDone(string file, IReadOnlyCollection<string> subtitleLanguages) =>
        L("done: ", "fertig: ", "готово: ") + file + SubtitlesSuffix(subtitleLanguages);

    public static string CliCookieStop => L(
        "Stopped: the cookies cannot be read. Try again without --cookies or with --cookies firefox.",
        "Gestoppt: Die Cookies lassen sich nicht lesen. Ohne --cookies oder mit --cookies firefox erneut versuchen.",
        "Остановлено: cookies не удаётся прочитать. Повторите без --cookies или с --cookies firefox.");

    public static string CliListOnly => L("List only: nothing was downloaded.", "Nur Liste: es wurde nichts heruntergeladen.", "Только список: ничего не скачано.");

    public static string CliFinished(int done, int failed, int skipped, int unavailable) => L(
        $"Finished: {done} downloaded, {failed} failed, {skipped} skipped, {unavailable} not available.",
        $"Fertig: {done} heruntergeladen, {failed} Fehler, {skipped} übersprungen, {unavailable} nicht verfügbar.",
        $"Готово: скачано {done}, ошибок {failed}, пропущено {skipped}, недоступно {unavailable}.");

    public static string CliLimitReached(int limit, int remaining) => L(
        $"Limit of {Videos(limit)} reached, {remaining} still open – the same command continues.",
        $"Limit von {Videos(limit)} erreicht, noch {remaining} offen – derselbe Aufruf macht weiter.",
        $"Достигнут лимит в {Videos(limit)}, осталось {remaining} – та же команда продолжит.");

    public static string CliSetupFailed(string message) => L("Setup failed: ", "Einrichten fehlgeschlagen: ", "Установка не удалась: ") + message;

    public static string CliStatus(string status) => status switch
    {
        "unavailable" => L("not available", "nicht verfügbar", "недоступно"),
        "skipped" => L("skipped", "übersprungen", "пропущено"),
        "exists" => L("exists", "vorhanden", "уже есть"),
        _ => L("new", "neu", "новое"),
    };

    public static string CliOutNeedsFolder => L("--out needs a folder.", "--out braucht einen Ordner.", "Для --out нужна папка.");
    public static string CliQualityValues => L("--quality: allowed are best, 1080, 720, 480 and audio.", "--quality: erlaubt sind best, 1080, 720, 480 und audio.", "--quality: допустимы best, 1080, 720, 480 и audio.");
    public static string CliSubsNeedLanguages => L("--subs needs languages, e.g. ru,de.", "--subs braucht Sprachen, z. B. ru,de.", "Для --subs нужны языки, например ru,de.");
    public static string CliLimitNeedsNumber => L("--limit needs a number from 0.", "--limit braucht eine Zahl ab 0.", "Для --limit нужно число от 0.");
    public static string CliPauseNeedsSeconds => L("--pause needs a number of seconds from 0.", "--pause braucht eine Anzahl Sekunden ab 0.", "Для --pause нужно число секунд от 0.");
    public static string CliCookiesNeedBrowser => L("--cookies needs a browser (firefox, chrome, edge, brave).", "--cookies braucht einen Browser (firefox, chrome, edge, brave).", "Для --cookies нужен браузер (firefox, chrome, edge, brave).");
    public static string CliLangValues => L("--lang: allowed are en, de and ru.", "--lang: erlaubt sind en, de und ru.", "--lang: допустимы en, de и ru.");
    public static string CliOptionNeedsValue(string option) => L($"{option} needs a value.", $"{option} braucht einen Wert.", $"Для {option} нужно значение.");
    public static string CliUnknownOption(string option) => L($"Unknown option: {option}", $"Unbekannte Option: {option}", $"Неизвестный параметр: {option}");

    public static string CliNeedLinkOrId => L(
        "Please give at least one link or ID. Help: Videolader.exe --help",
        "Bitte mindestens einen Link oder eine ID angeben. Hilfe: Videolader.exe --help",
        "Укажите хотя бы одну ссылку или ID. Справка: Videolader.exe --help");

    public static string CliHelp => L(
        """
        Videolader – download YouTube videos, playlists and channels

        Usage:
          Videolader.exe <link-or-ID> [<link-or-ID> ...] [options]
          Videolader.exe setup            download or update yt-dlp, ffmpeg and Deno
          Videolader.exe --help
          Videolader.exe --version

        Without arguments the window opens.

        Inputs (any number, also mixed):
          video link or video ID, playlist link or ID (PL...), channel link (https://www.youtube.com/@Name)
          or @Name. The same can be written with --id, --playlist or --url.

        Options (not given = value from the saved settings):
          --out <folder>       folder for the videos
          --quality <value>    best, 1080, 720, 480 or audio
          --subs <languages>   subtitle languages, e.g. ru,de
          --no-subs            no subtitles
          --limit <N>          at most N videos per run (0 = all)
          --pause <seconds>    pause between two downloads
          --cookies <browser>  firefox, chrome, edge or brave (only firefox works reliably)
          --no-skip            download videos that already exist again
          --list               only list, download nothing
          --json               one JSON line per video on stdout, messages on stderr
          --verbose            also show the output of yt-dlp
          --lang <code>        language of the messages: en, de or ru

        Exit codes: 0 = all fine, 1 = at least one error, 2 = wrong input,
                    3 = tools missing (then "Videolader.exe setup"), 130 = cancelled.

        Note: Videolader.exe is a Windows application. In the console the prompt returns
        immediately, the output still appears. To wait: "start /wait Videolader.exe ..." (cmd)
        or "Videolader.exe ... | Out-Host" (PowerShell).
        """,
        """
        Videolader – YouTube-Videos, Playlists und Kanäle herunterladen

        Aufruf:
          Videolader.exe <Link-oder-ID> [<Link-oder-ID> ...] [Optionen]
          Videolader.exe setup            yt-dlp, ffmpeg und Deno herunterladen bzw. aktualisieren
          Videolader.exe --help
          Videolader.exe --version

        Ohne Argumente startet die Oberfläche.

        Eingaben (beliebig viele, auch gemischt):
          Video-Link oder Video-ID, Playlist-Link oder -ID (PL...), Kanal-Link (https://www.youtube.com/@Name)
          oder @Name. Mit --id, --playlist oder --url lässt sich dasselbe schreiben.

        Optionen (nicht angegeben = Wert aus den gespeicherten Einstellungen):
          --out <Ordner>       Zielordner
          --quality <Wert>     best, 1080, 720, 480 oder audio
          --subs <Sprachen>    Untertitel-Sprachen, z. B. ru,de
          --no-subs            keine Untertitel
          --limit <N>          höchstens N Videos pro Lauf (0 = alle)
          --pause <Sekunden>   Pause zwischen zwei Downloads
          --cookies <Browser>  firefox, chrome, edge oder brave (nur firefox klappt zuverlässig)
          --no-skip            auch bereits vorhandene Videos erneut laden
          --list               nur auflisten, nichts herunterladen
          --json               pro Video eine JSON-Zeile auf stdout, Meldungen auf stderr
          --verbose            auch die Ausgabe von yt-dlp zeigen
          --lang <Code>        Sprache der Meldungen: en, de oder ru

        Exitcodes: 0 = alles in Ordnung, 1 = mindestens ein Fehler, 2 = falsche Eingabe,
                   3 = Werkzeuge fehlen (dann "Videolader.exe setup"), 130 = abgebrochen.

        Hinweis: Videolader.exe ist eine Windows-Anwendung. In der Konsole kehrt die Eingabeaufforderung
        sofort zurück, die Ausgabe erscheint trotzdem. Zum Warten: "start /wait Videolader.exe ..." (cmd)
        oder "Videolader.exe ... | Out-Host" (PowerShell).
        """,
        """
        Videolader – скачивание видео, плейлистов и каналов YouTube

        Вызов:
          Videolader.exe <ссылка-или-ID> [<ссылка-или-ID> ...] [параметры]
          Videolader.exe setup            скачать или обновить yt-dlp, ffmpeg и Deno
          Videolader.exe --help
          Videolader.exe --version

        Без аргументов открывается окно программы.

        Ввод (сколько угодно, можно вперемешку):
          ссылка или ID видео, ссылка или ID плейлиста (PL...), ссылка на канал (https://www.youtube.com/@Name)
          или @Name. То же можно указать через --id, --playlist или --url.

        Параметры (не указан = значение из сохранённых настроек):
          --out <папка>        папка для видео
          --quality <значение> best, 1080, 720, 480 или audio
          --subs <языки>       языки субтитров, например ru,de
          --no-subs            без субтитров
          --limit <N>          не больше N видео за запуск (0 = все)
          --pause <секунды>    пауза между двумя загрузками
          --cookies <браузер>  firefox, chrome, edge или brave (надёжно работает только firefox)
          --no-skip            заново скачивать уже имеющиеся видео
          --list               только показать список, ничего не скачивать
          --json               по одной строке JSON на видео в stdout, сообщения в stderr
          --verbose            показывать также вывод yt-dlp
          --lang <код>         язык сообщений: en, de или ru

        Коды выхода: 0 = всё в порядке, 1 = хотя бы одна ошибка, 2 = неверный ввод,
                     3 = нет инструментов (тогда "Videolader.exe setup"), 130 = отменено.

        Примечание: Videolader.exe – оконное приложение Windows. В консоли приглашение возвращается
        сразу, но вывод всё равно появляется. Чтобы дождаться: "start /wait Videolader.exe ..." (cmd)
        или "Videolader.exe ... | Out-Host" (PowerShell).
        """);

    // ----- Numbers and plurals -----

    /// <summary>"1 video" / "3 videos"; Russian "видео" does not change.</summary>
    public static string Videos(int count) => L(
        Count(count, "video", "videos"),
        Count(count, "Video", "Videos"),
        $"{count} видео");

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Megabytes(long bytes) =>
        (bytes / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture) + L(" MB", " MB", " МБ");
}
