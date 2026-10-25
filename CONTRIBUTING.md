# Contributing to Videolader

Thank you for your interest in Videolader! Bug reports, ideas, translations and code are welcome.
Videolader is a small project maintained in spare time, so please read these few rules first.

## Reporting a problem or an idea

Open an [issue](https://github.com/Schelawski/Videolader/issues/new/choose). Helpful for problems:

- what you did, what you expected and what happened instead,
- the Videolader version and your Windows version,
- the lines from the log in the Videolader window (the error and the “Tip” below it),
- the link or ID of a **public** video or playlist that shows the problem.

Please do not post links to private videos, and never post cookies or your API key.

## Before you write code

**Please open an issue first and wait for a short answer** before starting larger changes (new features,
new dependencies, changes to the user interface or to the files Videolader writes). This avoids work that
cannot be merged. Small fixes (typos, obvious bugs) can go straight to a pull request.

## Pull requests

1. Fork the repository and create a branch from `develop` (not `master`).
2. Keep a pull request focused on one topic.
3. Build and test locally (see [Build](README.md#build)):
   ```powershell
   dotnet build
   dotnet test
   ```
   The automatic build on GitHub runs the same tests; it starts after a maintainer has approved it for
   first-time contributors.
4. Describe what you changed and why, and link the issue (`Closes #123`).

The maintainer reviews every pull request and decides whether and when it is merged. Nothing is merged
automatically.

## Guidelines

- **Code, comments and repository documentation in English.** Match the style of the surrounding code.
  The READMEs exist in English, German and Russian; please update all three or say which ones are missing.
- **The user interface speaks English, German and Russian.** Every user-visible text – window, command line,
  log and error messages – lives in `src/Videolader.Core/UiText.cs` and is written as
  `L("English", "German", "Russian")`. A text in only one language does not compile. If you cannot translate
  into one of the languages, say so in the pull request – it will be completed before merging.
- **Plain language.** Videolader is made for people without technical knowledge. Explain what the user
  can do, not what went wrong inside.
- **Help texts** are in `src/Videolader.Core/Help/help.{en,de,ru}.md`. All three files keep the same topics
  in the same order with the same structure, and button names must match the user interface (the tests
  check this).
- **Privacy first.** No telemetry. Network access is limited to YouTube, the optional YouTube Data API and
  GitHub (tools). Downloaded programs are always checked against their published checksums.
- **Examples** in code, tests and documentation use freely licensed videos (e.g. the Blender open movies,
  `aqz-KE-bpKQ`), never copyrighted music videos.
- Add or update unit tests in `tests/Videolader.Tests` for changes in `src/Videolader.Core`.

## License of contributions

Videolader is licensed under the [GNU General Public License v3.0](LICENSE). By submitting a pull request
you agree that your contribution is published under the same license. You keep the copyright of your
contribution.
