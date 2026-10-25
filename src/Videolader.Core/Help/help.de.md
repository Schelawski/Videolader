Help texts of Videolader (German). Format: see src/Videolader.Core/HelpDocument.cs.
help.en.md and help.ru.md must contain the same topics in the same order, with the same number of paragraphs,
subheadings and list items per topic. Button names must match the user interface exactly (see HelpTests).

# about | Was macht Videolader?

Videolader lädt Videos, ganze Playlists und Kanäle von YouTube auf deinen Computer. Jedes Video wird nach seiner **Video-ID** benannt, z. B. `dQw4w9WgXcQ.mp4`. So bleibt der Name eindeutig, auch wenn der Titel Sonderzeichen enthält oder sich später ändert.

## Was pro Video entsteht

- `<ID>.mp4` – das Video (bei „Nur Audio (M4A)“: `<ID>.m4a`).
- `<ID>.info.json` – Titel, Kanal, Datum, Dauer, Beschreibung und Playlist.
- `<ID>.ru.json` und `<ID>.ru.srt` – die Untertitel, wenn gewünscht (je Sprache ein Paar).
- `videolader-archiv.txt` – die Liste aller heruntergeladenen IDs im Zielordner.

## Wer die eigentliche Arbeit macht

Das Herunterladen übernimmt das freie Programm yt-dlp. Videolader ist die einfache Oberfläche dafür: Es verwaltet die Liste, erkennt Vorhandenes und schreibt die Zusatzdateien.

## Rechtliches

YouTube erlaubt Downloads grundsätzlich nur über die eigenen Funktionen. Für eigene Inhalte oder mit Erlaubnis der Rechteinhaber ist das unproblematisch.

# start | Erste Schritte

1. **Links einfügen:** Kopiere Video-, Playlist- oder Kanal-Links in das Feld „Videos und Playlists“, einen pro Zeile. Auch reine IDs und `@Kanalname` funktionieren.
2. **Liste laden:** Klicke auf „Liste laden“ (oder Strg+Enter). Playlists werden aufgelöst, doppelte Videos zusammengefasst.
3. **Auswählen:** Jedes Video bekommt einen Status: „Neu“, „Bereits vorhanden“ oder „Nicht verfügbar“ (privat oder gelöscht). „Nur neue“ setzt die Häkchen bei allem, was noch fehlt.
4. **Herunterladen:** Klicke auf „Herunterladen“. Die Videos werden nacheinander geladen, mit einer Pause dazwischen. Ein Fehler stoppt die Liste nicht. Mit „Abbrechen“ hörst du jederzeit auf.

## Tipps für die Liste

- Die Leertaste setzt oder entfernt die Häkchen der markierten Zeilen.
- Ein Doppelklick öffnet das Video im Browser, ein Rechtsklick kopiert IDs oder Links.
- „Protokoll ausblenden“ gibt der Liste mehr Platz.

## Was du eingeben kannst

Video-Links (`watch?v=`, `youtu.be/`, `shorts/`, `live/`), Video-IDs, Playlist-Links und -IDs (`PL…`) sowie Kanäle (Link, `@Name` oder Kanal-ID `UC…`). Bei Kanälen wird der Reiter „Videos“ geladen. Shorts und Livestreams gibst du als eigene Zeile mit `…/shorts` bzw. `…/streams` an.

# existing | Bereits vorhanden und Archiv

Videolader lädt kein Video doppelt. „Bereits vorhanden“ heißt: Im Zielordner liegt schon `<ID>.mp4` (oder `.m4a`, `.mkv` …) **oder** die ID steht in der Datei `videolader-archiv.txt` im Zielordner. Solange „Vorhandene überspringen“ eingeschaltet ist, werden solche Videos nicht noch einmal geladen.

## Videos verschieben

Du kannst fertige Videos woandershin verschieben oder umbenennen. Weil ihre ID im Archiv steht, werden sie trotzdem nicht erneut geladen.

## Ein Video bewusst neu laden

Öffne `videolader-archiv.txt` mit dem Editor, lösche die Zeile mit der ID und speichere. Oder schalte „Vorhandene überspringen“ aus: Dann werden auch vorhandene Videos markiert und erneut geladen.

## Ein anderer Zielordner

Jeder Zielordner hat sein eigenes Archiv. Wechselst du den Ordner, prüft Videolader die Liste sofort neu.

# quality | Qualität und Untertitel

## Qualität

- „Bis 1080p (MP4)“ – der Standard: gute Qualität, auf jedem Gerät abspielbar.
- „Beste Qualität (MP4)“ – die höchste Auflösung, die YouTube anbietet. Die Dateien können sehr groß werden.
- „Bis 720p (MP4)“ und „Bis 480p (MP4)“ – kleinere Dateien, z. B. für Vorträge, bei denen das Bild nicht so wichtig ist.
- „Nur Audio (M4A)“ – nur der Ton, ideal zum Anhören oder für eine spätere Transkription.

Videolader bevorzugt H.264-Video und AAC-Ton, damit die Dateien überall laufen.

## Untertitel

Mit dem Häkchen „herunterladen“ bei „Untertitel“ lädt Videolader die Untertitel in den angegebenen Sprachen, z. B. `ru` oder `ru,de,en`. Die erste Sprache gilt auch für die Titel in der Liste.

- Vom Kanal hochgeladene Untertitel haben Vorrang vor YouTubes automatischen Untertiteln.
- Automatische Untertitel in einer fremden Sprache sind maschinelle Übersetzungen – sie können ungenau sein.
- Gespeichert wird im Whisper-Format (`<ID>.ru.json`) und als SRT (`<ID>.ru.srt`). Die doppelten Zeilen der „rollenden“ automatischen Untertitel entfernt Videolader.

# pause | Pause, Zufall und Limit

YouTube bemerkt, wenn viele Videos schnell hintereinander geladen werden, und sperrt die Downloads dann eine Zeit lang (Meldung „HTTP Error 429“ oder „Sign in to confirm you're not a bot“). Die Pause zwischen zwei Downloads schützt davor.

## Erfahrungen aus der Praxis

- Mit **30 Sekunden** Pause sperrt YouTube ziemlich oft.
- Ab **45 Sekunden** ist eine Sperre selten. Selbst 100 Videos am Stück laufen dann normalerweise durch.
- Wichtiger als der genaue Takt ist vermutlich die Menge pro Zeit: Lieber weniger Videos pro Lauf als eine kürzere Pause.

## „zufällig bis +50 %“

Der eingestellte Wert ist die **Mindestpause**. Jede Pause wird zufällig um bis zu die Hälfte verlängert, damit kein gleichmäßiger Takt entsteht: Aus 45 Sekunden werden 45 bis 67 Sekunden. Unten in der Statuszeile steht die tatsächliche Pause.

## Limit

„Limit“ begrenzt, wie viele Videos pro Lauf geladen werden (0 = alle markierten). Stell bei langen Listen z. B. 50 ein und klicke später einfach erneut auf „Herunterladen“: Fertige Videos sind dann „Bereits vorhanden“ und werden übersprungen.

## Wenn YouTube trotzdem sperrt

Warte eine Weile (oft reichen ein paar Stunden), erhöhe die Pause und mach dann mit derselben Liste weiter.

# cookies | Cookies aus Browser

**Meist brauchst du keine Cookies.** Öffentliche Videos und Playlists lädt Videolader ohne Anmeldung. Cookies sind nur nötig, wenn YouTube „Sign in to confirm you're not a bot“ meldet, bei altersbeschränkten Videos oder bei Videos nur für Kanalmitglieder.

## So geht es mit Firefox

1. Melde dich in Firefox bei YouTube an.
2. Wähle unter „Einstellungen“ bei „Cookies aus Browser“ den Eintrag „Firefox“.
3. Firefox darf dabei geöffnet bleiben.

## Warum Chrome, Edge und Brave meist nicht gehen

Diese Browser sperren unter Windows ihre Cookie-Datei, solange sie laufen („Could not copy Chrome cookie database“). Seit Chrome 127 verschlüsseln sie die Cookies außerdem so, dass nur der Browser selbst sie lesen kann („Failed to decrypt with DPAPI“). Nach unserem Stand bleibt dieses zweite Problem auch bei geschlossenem Browser.

## Was Videolader dann tut

Kann Videolader die Cookies nicht lesen, stoppt der Download sofort – sonst würde jedes weitere Video genauso scheitern – und bietet an, die Cookies auszuschalten („keine“). Danach einfach erneut auf „Herunterladen“ klicken.

# apikey | YouTube-API-Key

Der Key ist **freiwillig**. Mit ihm liest Videolader Playlists und Titel über die offizielle Schnittstelle von YouTube – schneller und zuverlässiger. Zum Herunterladen wird er nicht gebraucht, denn die Schnittstelle liefert keine Videodateien. Ohne Key übernimmt das yt-dlp.

## Einen Key anlegen

1. Öffne `console.cloud.google.com` und melde dich mit einem Google-Konto an.
2. Lege ein Projekt an und aktiviere darin die „YouTube Data API v3“.
3. Erstelle unter „Anmeldedaten“ einen API-Schlüssel und kopiere ihn.
4. Füge ihn unter „Einstellungen“ bei „YouTube-API-Key“ ein.

Das kostenlose Kontingent reicht für den normalen Gebrauch locker: 50 Videos einer Playlist kosten eine Einheit von 10.000 pro Tag.

## Sicherheit

Videolader speichert den Key mit Windows verschlüsselt, nur dein Benutzerkonto kann ihn lesen. Auf einem anderen Rechner oder Konto musst du ihn neu eintragen.

# tools | Werkzeuge

Videolader braucht drei kostenlose Programme. Beim ersten Start fehlen sie. „Einrichten…“ unter „Einstellungen“ bei „Werkzeuge“ lädt sie von den offiziellen Seiten auf GitHub:

- **yt-dlp** lädt die Videos.
- **ffmpeg** fügt Bild und Ton zusammen (ca. 190 MB).
- **Deno** löst eine JavaScript-Aufgabe, die YouTube seit Ende 2025 verlangt. Ohne Deno fehlen viele Formate.

## Aktuell halten

YouTube ändert sich oft, und ein veraltetes yt-dlp ist die häufigste Fehlerursache. Deshalb aktualisiert Videolader yt-dlp einmal am Tag beim Start. „Aktualisieren“ erledigt das sofort, auch für Deno.

## Wo sie liegen

Im Ordner `tools` neben `Videolader.exe` oder, falls dieser schreibgeschützt ist, in `%LOCALAPPDATA%\Videolader\tools`. Sind die Programme schon anderweitig installiert (im PATH), nutzt Videolader diese.

# cmdline | Kommandozeile

Mit Argumenten arbeitet `Videolader.exe` ohne Fenster – praktisch für die Aufgabenplanung und für Skripte. Nicht angegebene Optionen kommen aus den gespeicherten Einstellungen. Die Kommandozeile ändert diese nicht.

## Beispiele

- `Videolader.exe setup` – Werkzeuge einrichten (einmalig).
- `Videolader.exe dQw4w9WgXcQ --out D:\Videos` – ein Video.
- `Videolader.exe PLxxxxxxxx --limit 50 --pause 45` – eine Playlist, höchstens 50 pro Lauf.
- `Videolader.exe @Kanalname --list` – einen Kanal nur auflisten.
- `Videolader.exe --help --lang de` – alle Optionen, auf Deutsch.

## Auf das Ende warten

Die Eingabeaufforderung kehrt sofort zurück, die Ausgabe erscheint trotzdem. Zum Warten: `start /wait Videolader.exe …` (cmd) oder `Videolader.exe … | Out-Host` (PowerShell). Exitcodes: 0 = in Ordnung, 1 = mindestens ein Fehler, 2 = falsche Eingabe, 3 = Werkzeuge fehlen, 130 = abgebrochen.

# problems | Probleme und Lösungen

## „HTTP Error 429“ – YouTube drosselt

Es wurden zu viele Videos zu schnell geladen. Warte eine Weile, erhöhe die Pause auf mindestens 45 Sekunden und mach dann weiter (siehe „Pause, Zufall und Limit“).

## „Sign in to confirm you're not a bot“

YouTube verlangt eine Anmeldung. Oft hilft schon eine längere Pause. Sonst Cookies aus Firefox verwenden (siehe „Cookies aus Browser“).

## „Could not copy Chrome cookie database“

Chrome, Edge und Brave geben ihre Cookies nicht heraus. Stell die Cookies auf „keine“ oder nimm Firefox.

## Ein Video ist „Nicht verfügbar“

Es ist privat, gelöscht oder in deinem Land gesperrt. Videolader setzt dafür kein Häkchen.

## Werkzeuge fehlen oder ffmpeg/Deno melden Fehler

Klicke unter „Einstellungen“ bei „Werkzeuge“ auf „Einrichten…“ bzw. „Aktualisieren“.

## Plötzlich scheitern alle Downloads

Meist hat YouTube etwas geändert. Klicke unter „Einstellungen“ auf „Aktualisieren“, damit yt-dlp die neueste Version bekommt.

## Windows warnt beim ersten Start

Erscheint „Der Computer wurde durch Windows geschützt“, klicke auf „Weitere Informationen“ und dann auf „Trotzdem ausführen“. Windows zeigt das bei Programmen, die noch wenig verbreitet sind.

## Zielordner auf einem Netzlaufwerk

Das geht. Teildateien landen während des Downloads immer auf der lokalen Festplatte und kommen erst am Ende in den Zielordner.

# uninstall | Deinstallieren

Videolader installiert nichts in Windows. Zum Entfernen löschst du:

1. `Videolader.exe` und daneben `Videolader.settings.json` und den Ordner `tools`,
2. falls vorhanden, den Ordner `%LOCALAPPDATA%\Videolader` (Werkzeuge und Zwischendateien),
3. falls vorhanden, den Ordner `%APPDATA%\Videolader` (dort liegen die Einstellungen, wenn der Ordner von Videolader.exe schreibgeschützt war).

Deine Videos und die Datei `videolader-archiv.txt` in den Zielordnern bleiben unberührt.

# licenses | Lizenzen und Danksagung

© 2026 A. Schelawski. Videolader ist freie Software unter der **GNU General Public License, Version 3**. Der Quelltext ist öffentlich: `github.com/Schelawski/Videolader`.

Videolader baut auf der Arbeit anderer auf. Herzlichen Dank an:

- **yt-dlp** – lädt die Videos (Unlicense).
- **FFmpeg** – fügt Bild und Ton zusammen (GPL, Build des yt-dlp-Teams).
- **Deno** – JavaScript-Laufzeit für YouTubes Prüfung (MIT-Lizenz).
