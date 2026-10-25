# Changelog

All notable changes to Videolader. The section of a version becomes its release notes on GitHub
(see [Releasing](README.md#releasing)). Versions follow [semantic versioning](https://semver.org/).

## [Unreleased]

## [1.0.0]

The first public version of Videolader.

### Downloading
- Download single videos, whole playlists and channels: paste links or IDs, one per line. Playlists are
  expanded and duplicates merged.
- Files are named by video ID, with `<ID>.info.json` (title, channel, date, duration, description, playlist)
  next to the video. Quality from “Up to 480p” to “Best quality”, or audio only (M4A).
- Subtitles in the chosen languages as Whisper JSON and SRT, without the duplicate lines of YouTube's
  rolling automatic subtitles.
- Never twice: videos in the folder or in `videolader-archiv.txt` are skipped, even after moving them.
- Pause between downloads with a random extra of up to 50 %, and a limit per run, so YouTube does not block
  the downloads.
- Stops right away when the browser cookies cannot be read (Chrome, Edge and Brave lock them on Windows) and
  offers to turn them off.
- Partial files always stay on the local disk, also when the folder is on a network drive.

### Setup and safety
- Downloads yt-dlp, ffmpeg and Deno itself and checks each download against the SHA-256 checksum the project
  publishes; yt-dlp is updated once a day.
- The optional YouTube API key is stored encrypted with Windows.
- No telemetry; connections only to YouTube, the YouTube Data API (with your own key) and GitHub.

### Everyday use
- User interface, command line and help in English, German and Russian; the language follows Windows on the
  first start and can be switched under Settings → Language.
- Built-in help in plain language (“Help”, F1, “?” next to the settings), including experience from
  practice about pauses and cookies.
- The list gets most of the window; the log can be hidden. The version is shown in the window title.
- Command line for scripts and the Task Scheduler (`Videolader.exe --help`, `--version`, `--json`).
- One self-contained `Videolader.exe`, no installation needed.
