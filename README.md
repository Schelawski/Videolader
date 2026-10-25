# Videolader

[![Build](https://github.com/Schelawski/Videolader/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/Schelawski/Videolader/actions/workflows/build.yml)

Kleine Windows-Desktop-App, die YouTube-Videos und ganze Playlists herunterlädt. Die Dateien werden
nach der **Video-ID** benannt, daneben liegen eine Metadaten-Datei und auf Wunsch die Untertitel
im Whisper-JSON-Format. Bereits heruntergeladene Videos werden erkannt und übersprungen.

Unter der Haube arbeitet [yt-dlp](https://github.com/yt-dlp/yt-dlp). Die App steuert es, verwaltet
die Liste und schreibt die Zusatzdateien.

Oberfläche und Kommandozeile gibt es auf **Englisch, Deutsch und Russisch**. Beim ersten Start
richtet sich die Sprache nach Windows (Deutsch bzw. Russisch, sonst Englisch); umschalten lässt sie
sich unter **Einstellungen → Sprache**, auf der Kommandozeile mit `--lang en|de|ru`.

Eine **Hilfe** in allen drei Sprachen öffnet sich mit dem Knopf „Hilfe“ unten rechts, mit F1 (passend
zum Feld, in dem man gerade ist) oder über die kleinen „?“ neben den Einstellungen. Sie enthält auch
die Erfahrungen aus der Praxis, etwa zur Pause und zu den Cookies. Die Texte stehen in
`src/Videolader.Core/Help/help.{en,de,ru}.md`.

## Was pro Video entsteht

```
D:\Videos\Ananda\
  aqz-KE-bpKQ.mp4           Video (H.264 + AAC, bei „Nur Audio“: .m4a)
  aqz-KE-bpKQ.info.json     ID, Titel, Kanal, Datum, Dauer, Beschreibung, Playlist, Untertitel
  aqz-KE-bpKQ.ru.json       Untertitel im Whisper-Format (optional)
  aqz-KE-bpKQ.ru.srt        dieselben Untertitel als SRT (optional)
  videolader-archiv.txt     Liste aller heruntergeladenen IDs
```

Die Metadaten heißen bewusst `<ID>.info.json` und nicht `<ID>.json`. So kollidieren sie nicht mit
einem Transkript, das Wortlaut für `<ID>.mp4` als `<ID>.json` schreibt.

### `<ID>.info.json`

```json
{
  "id": "aqz-KE-bpKQ",
  "title": "Лекция 12",
  "url": "https://www.youtube.com/watch?v=aqz-KE-bpKQ",
  "channel": "Ananda Vidya",
  "channelId": "UC…",
  "uploadDate": "2024-05-01",
  "durationSeconds": 3725,
  "description": "…",
  "playlist": { "id": "PL…", "title": "Satsang 2026", "index": 3 },
  "file": "aqz-KE-bpKQ.mp4",
  "subtitles": [
    { "language": "ru", "automatic": true, "file": "aqz-KE-bpKQ.ru.json", "srt": "aqz-KE-bpKQ.ru.srt" }
  ],
  "downloadedAt": "2026-09-30T20:43:54+02:00"
}
```

`automatic: true` bedeutet: YouTubes automatische Spracherkennung. `false` bedeutet: vom Kanal
hochgeladene Untertitel.

### `<ID>.<Sprache>.json` (Whisper-Format)

```json
{
  "text": "Добрый вечер сегодня мы поговорим",
  "segments": [
    { "id": 0, "start": 0.16, "end": 2.96, "text": "Добрый вечер" },
    { "id": 1, "start": 2.96, "end": 6.96, "text": "сегодня мы поговорим" }
  ],
  "language": "ru"
}
```

Zeiten sind in Sekunden. YouTubes automatische Untertitel werden als „rollende“ zweizeilige
Einblendungen geliefert. Videolader entfernt die doppelten Zeilen und Überlappungen, sodass jede
Zeile genau einmal vorkommt.

## Bedienung

1. Links oder IDs in das obere Feld einfügen, eine pro Zeile. Erkannt werden:
   Video-Links (`watch?v=`, `youtu.be/`, `shorts/`, `live/`), Video-IDs, Playlist-Links und
   Playlist-IDs (`PL…`) sowie Kanäle (Link, `@name` oder Kanal-ID `UC…`, dann wird der Tab
   „Videos“ geladen; Shorts und Livestreams sind eigene Tabs und müssen als eigene Zeile
   `…/shorts` bzw. `…/streams` angegeben werden).
2. **Liste laden** (oder Strg+Enter). Playlists werden aufgelöst, doppelte Videos zusammengefasst.
   Jedes Video bekommt einen Status: **Neu**, **Bereits vorhanden** oder **Nicht verfügbar**
   (privat oder gelöscht).
3. Häkchen prüfen. „Nur neue“ wählt alles, was noch fehlt. Leertaste und Rechtsklick wirken auf
   markierte Zeilen, Doppelklick öffnet das Video im Browser.
4. **Herunterladen**. Die Videos werden nacheinander geladen, mit einer einstellbaren Pause
   dazwischen. Ein Fehler stoppt die Liste nicht.

„Bereits vorhanden“ heißt: Im Zielordner liegt `<ID>.mp4` (oder `.m4a`, `.mkv` …) **oder** die ID
steht in `videolader-archiv.txt`. Du kannst fertige Videos also woandershin verschieben und sie
werden trotzdem nicht erneut geladen. Wenn du ein Video bewusst nochmal willst, löschst du die
Zeile aus der Archivdatei.

### Einstellungen

Zielordner, Qualität, Untertitel, Pause und Limit stehen auf der Registerkarte **Download** neben
dem Eingabefeld. Selten Gebrauchtes (Sprache, Cookies, API-Key, Werkzeuge) liegt auf der Registerkarte
**Einstellungen**. Das Protokoll unter der Liste lässt sich mit „Protokoll ausblenden“ wegklappen,
dann hat die Liste mehr Platz.

| Einstellung | Bedeutung |
|---|---|
| Zielordner | Hier landen die Videos. Standard: `Videos\Videolader`. |
| Qualität | Beste, bis 1080p (Standard), bis 720p, bis 480p oder nur Audio. |
| Cookies aus Browser | Nur nötig, wenn YouTube „Sign in to confirm you're not a bot“ meldet. Firefox funktioniert am zuverlässigsten, denn Chrome, Edge und Brave sperren ihre Cookies unter Windows, solange der Browser läuft, und verschlüsseln sie so, dass yt-dlp sie meist nicht lesen kann („Could not copy Chrome cookie database“). Dann stoppt der Download sofort und Videolader bietet an, die Cookies auszuschalten. |
| Untertitel / Sprachen | z. B. `ru` oder `ru,de,en`. Hochgeladene Untertitel haben Vorrang vor automatischen. Achtung: Bei automatischen Untertiteln liefert YouTube für fremde Sprachen maschinelle Übersetzungen. |
| Pause | Sekunden zwischen zwei Downloads, schützt vor YouTubes Drosselung (HTTP 429). Mit „zufällig bis +50 %“ (Standard) ist der eingestellte Wert die Mindestpause, jede Pause wird zufällig um bis zu die Hälfte verlängert (45 s → 45 bis 67 s), damit kein gleichmäßiger Takt entsteht. Gilt auch für die Kommandozeile. |
| Limit | Höchstens so viele Videos pro Lauf (0 = alle). Bei langen Listen z. B. 50 und beim nächsten Mal einfach weitermachen: Fertige Videos stehen im Archiv und werden übersprungen. |
| YouTube-API-Key | Optional. Damit werden Playlists und Titel über die offizielle API gelesen (schnell, 1 Quota-Einheit pro 50 Videos). Ohne Key übernimmt das yt-dlp bzw. YouTubes oEmbed-Schnittstelle. **Zum Herunterladen wird der Key nicht gebraucht**, weil die API keine Videodateien liefert. |

Die Einstellungen stehen in `Videolader.settings.json` neben der exe (oder in
`%APPDATA%\Videolader`, wenn der Ordner schreibgeschützt ist). Der API-Key wird dort mit Windows
DPAPI für dein Benutzerkonto verschlüsselt; auf einem anderen Konto oder Rechner muss er neu
eingetragen werden.

## Kommandozeile

Mit Argumenten arbeitet `Videolader.exe` ohne Fenster, ohne Argumente öffnet sich die Oberfläche.

```powershell
Videolader.exe setup                                   # yt-dlp, ffmpeg, Deno einrichten (einmalig)
Videolader.exe aqz-KE-bpKQ --out D:\Videos             # ein Video
Videolader.exe PLxxxxxxxxxxx --limit 50 --pause 5      # eine Playlist, höchstens 50 pro Lauf
Videolader.exe @KanalName --list                       # Kanal nur auflisten
Videolader.exe --help --lang en                        # alle Optionen, auf Englisch
```

Nicht angegebene Optionen kommen aus den gespeicherten Einstellungen. Die Kommandozeile ändert
diese Einstellungen nicht. Mit `--json` schreibt Videolader pro Video eine JSON-Zeile auf stdout
(`id`, `status`, `title`, `file`, `error`), alle Meldungen gehen dann nach stderr. Exitcodes:
0 = in Ordnung, 1 = mindestens ein Fehler, 2 = falsche Eingabe, 3 = Werkzeuge fehlen,
130 = abgebrochen (Strg+C).

Die exe ist eine Windows-Anwendung ohne eigene Konsole. Sie hängt sich an die Konsole an, aus der
sie gestartet wurde, aber die Eingabeaufforderung kehrt sofort zurück. Zum Warten auf das Ende
(und für den Exitcode): `start /wait Videolader.exe …` in cmd bzw. `Videolader.exe … | Out-Host`
in PowerShell. Bei umgeleiteter Ausgabe (`> datei.txt`, Pipe) wird die Umleitung verwendet.

## Werkzeuge

Beim ersten Start fehlen drei Programme. **Einstellungen → Werkzeuge → Einrichten…** lädt sie von den offiziellen
GitHub-Releases in den Ordner `tools` neben der exe (oder nach `%LOCALAPPDATA%\Videolader\tools`):

| Programm | Wofür |
|---|---|
| `yt-dlp.exe` | lädt die Videos |
| `ffmpeg.exe`, `ffprobe.exe` | führt Video- und Tonspur zusammen (ca. 190 MB, Build des yt-dlp-Teams) |
| `deno.exe` | JavaScript-Laufzeit. YouTube verlangt seit Ende 2025, dass yt-dlp eine JavaScript-Aufgabe löst, sonst fehlen viele Formate. |

Liegen die Programme schon im PATH, werden sie verwendet. yt-dlp wird einmal täglich beim Start
automatisch aktualisiert, denn YouTube ändert sich oft, und ein veraltetes yt-dlp ist die häufigste
Fehlerursache. **Aktualisieren** erzwingt das sofort (inklusive Deno).

## Bauen

Voraussetzung: .NET 10 SDK auf Windows.

```powershell
dotnet test
dotnet publish src\Videolader -c Release -o publish
```

Ergebnis: `publish\Videolader.exe`, eine einzelne Datei mit eingebettetem .NET, ohne Installation.

## Aufbau

```
src/Videolader.Core   Logik ohne Oberfläche (net10.0, läuft auch in Tests unter Linux)
  InputParser         Links/IDs → Video, Playlist, Kanal
  VideoResolver       Playlists auflösen, Titel holen (API → yt-dlp/oEmbed als Rückfall)
  YtDlpCommandLine    alle yt-dlp-Argumente an einer Stelle
  DownloadJob         ein Video laden, info.json und Untertitel schreiben, Archiv pflegen
  Subtitles           json3/VTT → Segmente → Whisper-JSON und SRT
  VideoLibrary        „schon vorhanden?“ (Datei oder Archiv)
  ToolLocator/-Installer  yt-dlp, ffmpeg, Deno finden bzw. herunterladen
src/Videolader        WinForms-Oberfläche (MainForm)
tests/Videolader.Tests  xUnit-Tests
```

Teildateien und Rohdaten landen während des Downloads immer auf der lokalen Platte in
`%LOCALAPPDATA%\Videolader\temp` und werden danach aufgeräumt, auch wenn der Zielordner ein
Netzlaufwerk ist (dort scheitert yt-dlp schon beim Schreiben kleiner Dateien mit „Errno 22“).
Abgebrochene Downloads setzen beim nächsten Versuch dort fort. Ein `.videolader-temp` in einem
Zielordner stammt aus einer älteren Version und kann gelöscht werden.

## Rechtliches

Videolader ist ein unabhängiges Projekt und **nicht mit YouTube oder Google verbunden**. „YouTube“
ist eine Marke von Google LLC.

Du bist selbst dafür verantwortlich, beim Herunterladen das Urheberrecht und die
Nutzungsbedingungen von YouTube einzuhalten. Diese erlauben Downloads grundsätzlich nur über
YouTubes eigene Funktionen. Unproblematisch ist es bei eigenen Inhalten, bei freien Inhalten
(z. B. unter Creative-Commons-Lizenz) oder mit Erlaubnis der Rechteinhaber.

Das Beispiel-Video `aqz-KE-bpKQ` in dieser README und in der Hilfe ist „Big Buck Bunny“ der
Blender Foundation (Creative Commons CC BY 3.0).

## Datenschutz

Videolader sammelt keine Daten und sendet nichts an den Entwickler: kein Konto, keine Statistik,
keine Werbung. Verbindungen gibt es nur zu:

- **YouTube**, um Listen, Titel und Videos zu laden,
- der **YouTube Data API** von Google, nur mit eigenem API-Key,
- **GitHub**, um yt-dlp, ffmpeg und Deno herunterzuladen und zu aktualisieren.

Cookies aus dem Browser werden nur gelesen, wenn ein Browser ausgewählt ist, und nur an yt-dlp
übergeben. Einstellungen und API-Key bleiben auf dem Rechner, der Key mit Windows DPAPI
verschlüsselt.

## Lizenzen

Videolader steht unter der [GNU General Public License, Version 3](LICENSE). Die Werkzeuge werden
nicht mitgeliefert, sondern beim Einrichten von ihren offiziellen Seiten geladen und stehen unter
eigenen Lizenzen:

| Werkzeug | Lizenz | Quelle |
|---|---|---|
| yt-dlp | [Unlicense](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE) | [github.com/yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp) |
| FFmpeg | [GPL](https://github.com/yt-dlp/FFmpeg-Builds#ffmpeg-static-auto-builds) (Build des yt-dlp-Teams) | [github.com/yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds) |
| Deno | [MIT](https://github.com/denoland/deno/blob/main/LICENSE.md) | [github.com/denoland/deno](https://github.com/denoland/deno) |
