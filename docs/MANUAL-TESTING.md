# Manuelle Tests unter Windows

Die Kernlogik ist durch `dotnet test` abgedeckt. Die Oberfläche und der echte Kontakt zu YouTube
lassen sich nur von Hand prüfen. Vor jeder Veröffentlichung einmal mit der Release-exe durchgehen.
Als Beispiele eignen sich die freien Blender-Filme, z. B. `aqz-KE-bpKQ` (Big Buck Bunny) oder die
Playlist des Kanals `@BlenderOfficial`.

## Einrichtung
- [ ] `Videolader.exe` in einen leeren Ordner legen und starten. Die Registerkarte heißt
      „Einstellungen – Werkzeuge fehlen“ (rotes Warnsymbol) und ist geöffnet, das Protokoll nennt die fehlenden Werkzeuge.
- [ ] Exe-Datei und Fenster zeigen das rote Videolader-Icon.
- [ ] **Einstellungen → Werkzeuge → Einrichten…** → Rückfrage bestätigen. yt-dlp, ffmpeg und Deno werden mit
      Fortschritt geladen und gegen ihre Prüfsummen geprüft, danach steht „yt-dlp ✓ ffmpeg ✓ Deno ✓“ (grün)
      und die yt-dlp-Version im Protokoll; der Reiter heißt wieder „Einstellungen“.
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

## Pause, Zufall und Limit
- [ ] Pause 45 s mit „zufällig bis +50 %“: Die Statuszeile zeigt zwischen den Videos unterschiedliche Pausen
      zwischen 45 und 67 s. Ohne Häkchen genau 45 s.
- [ ] Limit 2 bei 5 markierten Videos → nach 2 Downloads Stopp, Protokoll nennt das Limit; erneut
      **Herunterladen** macht mit den nächsten weiter.

## Cookies
- [ ] Cookies aus „Chrome (meist gesperrt)“ bei laufendem Chrome → der Download stoppt nach dem ersten Video
      mit einer Rückfrage „Cookies ausschalten?“; „Ja“ stellt auf „keine“, erneutes **Herunterladen** klappt.
- [ ] Dasselbe bei vollständig geschlossenem Chrome (auch kein Hintergrundprozess) – Ergebnis in #22 festhalten.
- [ ] Cookies aus Firefox (bei YouTube angemeldet) → Downloads laufen.

## Sprachen und Hilfe
- [ ] **Einstellungen → Sprache** auf English und auf Русский umstellen → das Fenster öffnet sich an derselben
      Stelle in der neuen Sprache, nichts ist abgeschnitten (auch bei kleinem Fenster).
- [ ] **Hilfe** unten rechts, F1 im Pausenfeld (öffnet „Pause, Zufall und Limit“) und ein „?“ neben den Cookies
      (Tooltip, Klick öffnet „Cookies aus Browser“) – in allen drei Sprachen.
- [ ] Nach einem Fehler mit Tipp im Protokoll: F1 im Protokoll öffnet das genannte Hilfethema.
- [ ] `Videolader.exe --help --lang ru` zeigt die russische Hilfe der Kommandozeile.

## Layout
- [ ] Fenster startet mittig mit fast voller Bildschirmhöhe; kleinste Größe (960 × 640) in allen drei Sprachen
      ohne abgeschnittene Texte.
- [ ] „Protokoll ausblenden“ klappt das Protokoll weg; nach einem Neustart bleibt es so.

## Abbrechen und Fehler
- [ ] Während eines großen Downloads **Abbrechen** → Status „Abgebrochen“, wartende Videos wieder „Neu“.
      Erneut starten → yt-dlp setzt die Teildatei fort.
- [ ] Fenster während eines Downloads schließen → Rückfrage; „Ja“ bricht ab und schließt.
- [ ] Ungültige Eingabe („hallo“) → Protokoll „Nicht erkannt: hallo“.

## Qualität
- [ ] „Nur Audio (M4A)“ → `<ID>.m4a` statt `.mp4`.
- [ ] „Bis 720p“ → Video hat höchstens 720 Zeilen.
