# Manuelle Tests unter Windows

Die Kernlogik ist durch `dotnet test` abgedeckt. Die Oberfläche und der echte Kontakt zu YouTube
lassen sich nur von Hand prüfen. Einmal nach dem ersten Build durchgehen:

## Einrichtung
- [ ] `Videolader.exe` in einen leeren Ordner legen und starten. Das Protokoll meldet fehlende Werkzeuge.
- [ ] **Einrichten…** → Rückfrage bestätigen. yt-dlp, ffmpeg und Deno werden mit Fortschritt geladen,
      danach steht „yt-dlp ✓ ffmpeg ✓ Deno ✓“ (grün) und die yt-dlp-Version im Protokoll.
- [ ] App neu starten. Einmal pro Tag läuft automatisch „Prüfe auf yt-dlp-Updates…“.

## Einzelne Videos
- [ ] Ein Video als Link und eines als nackte ID eintragen → **Liste laden**. Titel und Kanal erscheinen
      (ohne API-Key über oEmbed).
- [ ] **Herunterladen**. Status zeigt Prozent, dann „Zusammenführen…“, dann „Fertig + Untertitel ru“.
- [ ] Im Zielordner liegen `<ID>.mp4`, `<ID>.info.json`, `<ID>.ru.json`, `<ID>.ru.srt`, `videolader-archiv.txt`.
      Das Video lässt sich im Windows-Player abspielen (H.264/AAC).
- [ ] `<ID>.ru.json` öffnen: Segmente ohne doppelte Zeilen, Zeiten aufsteigend.

## Playlist und Duplikate
- [ ] Playlist-Link eintragen, der die schon geladenen Videos enthält → Liste laden.
      Die geladenen stehen auf „Bereits vorhanden“ und ohne Häkchen, der Rest auf „Neu“.
- [ ] Ein Video zusätzlich einzeln eintragen, das auch in der Playlist ist → es erscheint nur einmal,
      das Protokoll meldet „doppelte Einträge wurden zusammengefasst“.
- [ ] Eine `.mp4` in einen anderen Ordner verschieben → Liste neu laden → weiterhin „Bereits vorhanden“ (Archiv).
- [ ] Private/gelöschte Videos in der Playlist sind grau und „Nicht verfügbar“.

## API-Key
- [ ] Key eintragen → Playlist laden geht spürbar schneller, Dauer wird angezeigt.
- [ ] Falschen Key eintragen → Protokoll: „YouTube-API: API key not valid … versuche es mit yt-dlp“, Liste lädt trotzdem.

## Abbrechen und Fehler
- [ ] Während eines großen Downloads **Abbrechen** → Status „Abgebrochen“, wartende Videos wieder „Neu“.
      Erneut starten → yt-dlp setzt die Teildatei fort.
- [ ] Fenster während eines Downloads schließen → Rückfrage; „Ja“ bricht ab und schließt.
- [ ] Ungültige Eingabe („hallo“) → Protokoll „Nicht erkannt: hallo“.

## Qualität
- [ ] „Nur Audio (M4A)“ → `<ID>.m4a` statt `.mp4`.
- [ ] „Bis 720p“ → Video hat höchstens 720 Zeilen.
