Help texts of Videolader (English). Format: see src/Videolader.Core/HelpDocument.cs.
help.de.md and help.ru.md must contain the same topics in the same order, with the same number of paragraphs,
subheadings and list items per topic. Button names must match the user interface exactly (see HelpTests).

# about | What does Videolader do?

Videolader downloads videos, whole playlists and channels from YouTube to your computer. Each video is named after its **video ID**, e.g. `aqz-KE-bpKQ.mp4`. That keeps the name unique, even if the title contains special characters or changes later.

## What you get for each video

- `<ID>.mp4` – the video (with “Audio only (M4A)”: `<ID>.m4a`).
- `<ID>.info.json` – title, channel, date, duration, description and playlist.
- `<ID>.ru.json` and `<ID>.ru.srt` – the subtitles, if wanted (one pair per language).
- `videolader-archiv.txt` – the list of all downloaded IDs in the folder.

## Who does the actual work

The downloading is done by the free program yt-dlp. Videolader is the simple interface for it: it manages the list, recognizes what you already have and writes the extra files.

## Legal

Videolader is an independent project and **not affiliated with YouTube or Google**. “YouTube” is a trademark of Google LLC.

You are responsible for respecting copyright and YouTube's terms of service when downloading. YouTube generally allows downloads only through its own features. It is no problem for your own content, for free content (e.g. under a Creative Commons license) or with the permission of the rights holders.

## Privacy

Videolader collects no data about you and sends nothing to the developer: no account, no statistics, no ads. It only connects to:

- **YouTube** – to load lists, titles and videos.
- **YouTube Data API** by Google – only if you enter your own API key.
- **GitHub** – to download and update yt-dlp, ffmpeg and Deno.

Videolader reads cookies from the browser only if you choose a browser, and passes them only to yt-dlp, which uses them for its requests to YouTube. Settings and API key stay on your computer, the key encrypted.

# start | First steps

1. **Paste links:** Copy video, playlist or channel links into the box “Videos and playlists”, one per line. Plain IDs and `@ChannelName` work too.
2. **Load the list:** Click “Load list” (or Ctrl+Enter). Playlists are expanded, duplicate videos merged.
3. **Choose:** Each video gets a status: “New”, “Already there” or “Not available” (private or deleted). “Only new” checks everything that is still missing.
4. **Download:** Click “Download”. The videos are downloaded one after another, with a pause in between. An error does not stop the list. “Cancel” stops at any time.

## Tips for the list

- The space bar checks or unchecks the selected rows.
- A double click opens the video in the browser, a right click copies IDs or links.
- “Hide log” gives the list more room.

## What you can enter

Video links (`watch?v=`, `youtu.be/`, `shorts/`, `live/`), video IDs, playlist links and IDs (`PL…`) and channels (link, `@Name` or channel ID `UC…`). For channels the “Videos” tab is loaded. Enter Shorts and live streams as a separate line with `…/shorts` or `…/streams`.

# existing | Already there and the archive

Videolader never downloads a video twice. “Already there” means: the folder already contains `<ID>.mp4` (or `.m4a`, `.mkv` …) **or** the ID is listed in the file `videolader-archiv.txt` in the folder. As long as “Skip existing” is on, such videos are not downloaded again.

## Moving videos

You can move or rename finished videos. Because their ID is in the archive, they are still not downloaded again.

## Downloading a video again on purpose

Open `videolader-archiv.txt` in Notepad, delete the line with the ID and save. Or turn “Skip existing” off: then existing videos are checked too and downloaded again.

## A different folder

Each folder has its own archive. When you switch the folder, Videolader checks the list again right away.

# quality | Quality and subtitles

## Quality

- “Up to 1080p (MP4)” – the default: good quality, plays on every device.
- “Best quality (MP4)” – the highest resolution YouTube offers. The files can get very large.
- “Up to 720p (MP4)” and “Up to 480p (MP4)” – smaller files, e.g. for talks where the picture matters less.
- “Audio only (M4A)” – just the sound, ideal for listening or for a later transcription.

Videolader prefers H.264 video and AAC sound, so the files play everywhere.

## Subtitles

With “download” checked next to “Subtitles”, Videolader downloads the subtitles in the languages given, e.g. `ru` or `ru,de,en`. The first language is also used for the titles in the list.

- Subtitles uploaded by the channel take priority over YouTube's automatic subtitles.
- Automatic subtitles in a foreign language are machine translations – they can be inaccurate.
- They are saved in Whisper format (`<ID>.ru.json`) and as SRT (`<ID>.ru.srt`). Videolader removes the duplicate lines of the “rolling” automatic subtitles.

# pause | Pause, randomness and limit

YouTube notices when many videos are downloaded quickly one after another and then blocks the downloads for a while (message “HTTP Error 429” or “Sign in to confirm you're not a bot”). The pause between two downloads protects against this.

## Experience from practice

- With a pause of **30 seconds** YouTube blocks quite often.
- From **45 seconds** on a block is rare. Even 100 videos in a row usually work then.
- The amount per time probably matters more than the exact rhythm: rather fewer videos per run than a shorter pause.

## “random up to +50%”

The value you set is the **minimum pause**. Each pause is lengthened by a random amount of up to half, so there is no regular rhythm: 45 seconds become 45 to 67 seconds. The status line at the bottom shows the actual pause.

## Limit

“Limit” sets how many videos are downloaded per run (0 = all checked). For long lists set e.g. 50 and later simply click “Download” again: finished videos are then “Already there” and are skipped.

## If YouTube blocks anyway

Wait a while (a few hours are often enough), increase the pause and then continue with the same list.

# cookies | Cookies from browser

**Usually you need no cookies.** Videolader downloads public videos and playlists without signing in. Cookies are only needed if YouTube reports “Sign in to confirm you're not a bot”, for age-restricted videos or for members-only videos.

## How it works with Firefox

1. Sign in to YouTube in Firefox.
2. Under “Settings”, choose “Firefox” for “Cookies from browser”.
3. Firefox may stay open.

## Why Chrome, Edge and Brave usually don't work

On Windows these browsers lock their cookie file while they are running (“Could not copy Chrome cookie database”). Since Chrome 127 they also encrypt the cookies so that only the browser itself can read them (“Failed to decrypt with DPAPI”). As far as we know, this second problem remains even when the browser is closed.

## What Videolader does then

If Videolader cannot read the cookies, the download stops right away – otherwise every further video would fail the same way – and Videolader offers to turn the cookies off (“none”). Then simply click “Download” again.

# apikey | YouTube API key

The key is **optional**. With it Videolader reads playlists and titles through YouTube's official interface – faster and more reliably. It is not needed for downloading, because the interface delivers no video files. Without a key, yt-dlp does this.

## Creating a key

1. Open `console.cloud.google.com` and sign in with a Google account.
2. Create a project and enable the “YouTube Data API v3” in it.
3. Under “Credentials”, create an API key and copy it.
4. Paste it under “Settings” next to “YouTube API key”.

The free quota is plenty for normal use: 50 videos of a playlist cost one unit out of 10,000 per day.

## Security

Videolader stores the key encrypted by Windows; only your user account can read it. On another computer or account you have to enter it again.

# tools | Tools

Videolader needs three free programs. They are missing at the first start. “Set up…” under “Settings” next to “Tools” downloads them from their official pages on GitHub:

- **yt-dlp** downloads the videos.
- **ffmpeg** joins picture and sound (about 190 MB).
- **Deno** solves a JavaScript task that YouTube has required since late 2025. Without Deno many formats are missing.

## Keeping them up to date

YouTube changes often, and an outdated yt-dlp is the most common cause of errors. That's why Videolader updates yt-dlp once a day at start. “Update” does it right away, for Deno too.

## Where they are

In the folder `tools` next to `Videolader.exe` or, if that one is read-only, in `%LOCALAPPDATA%\Videolader\tools`. If the programs are already installed elsewhere (in the PATH), Videolader uses those.

# cmdline | Command line

With arguments `Videolader.exe` works without a window – handy for the Task Scheduler and for scripts. Options that are not given come from the saved settings. The command line does not change them.

## Examples

- `Videolader.exe setup` – set up the tools (once).
- `Videolader.exe aqz-KE-bpKQ --out D:\Videos` – one video.
- `Videolader.exe PLxxxxxxxx --limit 50 --pause 45` – one playlist, at most 50 per run.
- `Videolader.exe @ChannelName --list` – only list a channel.
- `Videolader.exe --help --lang en` – all options, in English.

## Waiting for the end

The prompt returns immediately, the output still appears. To wait: `start /wait Videolader.exe …` (cmd) or `Videolader.exe … | Out-Host` (PowerShell). Exit codes: 0 = all fine, 1 = at least one error, 2 = wrong input, 3 = tools missing, 130 = cancelled.

# problems | Problems and solutions

## “HTTP Error 429” – YouTube is throttling

Too many videos were downloaded too fast. Wait a while, increase the pause to at least 45 seconds and then continue (see “Pause, randomness and limit”).

## “Sign in to confirm you're not a bot”

YouTube asks you to sign in. A longer pause often helps already. Otherwise use cookies from Firefox (see “Cookies from browser”).

## “Could not copy Chrome cookie database”

Chrome, Edge and Brave do not hand out their cookies. Set cookies to “none” or use Firefox.

## A video is “Not available”

It is private, deleted or blocked in your country. Videolader does not check it.

## Tools are missing or ffmpeg/Deno report errors

Under “Settings”, click “Set up…” or “Update” next to “Tools”.

## Suddenly all downloads fail

Usually YouTube has changed something. Under “Settings”, click “Update” so that yt-dlp gets the newest version.

## Windows warns at the first start

If “Windows protected your PC” appears, click “More info” and then “Run anyway”. Windows shows this for programs that are not widely used yet.

## Folder on a network drive

That works. During the download, partial files always stay on the local disk and only go to the folder at the end.

# uninstall | Uninstalling

Videolader installs nothing in Windows. To remove it, delete:

1. `Videolader.exe` and next to it `Videolader.settings.json` and the folder `tools`,
2. if present, the folder `%LOCALAPPDATA%\Videolader` (tools and temporary files),
3. if present, the folder `%APPDATA%\Videolader` (the settings are there if the folder of Videolader.exe was read-only).

Your videos and the file `videolader-archiv.txt` in the folders stay untouched.

# licenses | Licenses and thanks

© 2026 A. Schelawski. Videolader is free software under the **GNU General Public License, version 3**. The source code is public: `github.com/Schelawski/Videolader`.

Videolader builds on the work of others. Many thanks to:

- **yt-dlp** – downloads the videos (Unlicense, `github.com/yt-dlp/yt-dlp`).
- **FFmpeg** – joins picture and sound (GPL, build by the yt-dlp team: `github.com/yt-dlp/FFmpeg-Builds`).
- **Deno** – JavaScript runtime for YouTube's check (MIT license, `github.com/denoland/deno`).

Videolader does not ship these tools. When you set them up, they are downloaded directly from the official pages above and compared with the checksum (SHA-256) each project publishes there. If it does not match, nothing is installed. You also find their licenses there. The example video `aqz-KE-bpKQ` in this help is “Big Buck Bunny” by the Blender Foundation (Creative Commons CC BY 3.0).
