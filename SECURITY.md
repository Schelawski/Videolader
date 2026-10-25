# Security policy

## Supported versions

Only the [newest release](https://github.com/Schelawski/Videolader/releases/latest) receives fixes. Please
check that a problem still occurs with it before reporting.

## Reporting a vulnerability

**Please do not open a public issue for security problems.** Report them privately instead:

1. Open the [Security tab](https://github.com/Schelawski/Videolader/security) of this repository.
2. Click **Report a vulnerability** and describe the problem, how to reproduce it and what an attacker
   could do with it.

You will get an answer as soon as possible, usually within a week. Once a fix is released, the report is
published together with the release notes, and you are credited if you wish.

## Scope

Videolader downloads and starts yt-dlp, ffmpeg and Deno and checks each download against the SHA-256
checksum published by the project. Problems in these tools themselves should be reported to their
projects: [yt-dlp](https://github.com/yt-dlp/yt-dlp/security), [FFmpeg](https://ffmpeg.org/security.html),
[Deno](https://github.com/denoland/deno/security).
