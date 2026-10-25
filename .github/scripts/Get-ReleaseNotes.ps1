<#
.SYNOPSIS
    Writes the release notes of one version: its section from CHANGELOG.md plus download hints.

.DESCRIPTION
    The section starts at "## [1.2.0]" (optionally followed by " – date") and ends before the next "## ".
    Fails if the version has no section, so a release is never published without notes.
    Works with Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./.github/scripts/Get-ReleaseNotes.ps1 -Version 1.2.0 -Path CHANGELOG.md -OutFile release-notes.md
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [string] $OutFile
)

$ErrorActionPreference = 'Stop'

$lines = Get-Content -Path $Path -Encoding UTF8
$header = '^##\s+\[' + [regex]::Escape($Version) + '\]'
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $header) { $start = $i + 1; break }
}
if ($start -lt 0) {
    throw "CHANGELOG.md has no section '## [$Version]'. Add it before tagging the release."
}

$section = New-Object System.Collections.Generic.List[string]
for ($i = $start; $i -lt $lines.Count -and $lines[$i] -notmatch '^##\s'; $i++) {
    $section.Add($lines[$i])
}
$body = ($section -join "`n").Trim()
if ($body.Length -eq 0) {
    throw "The section '## [$Version]' in CHANGELOG.md is empty."
}

$footer = @'

---

**Download:** `Videolader.exe` below – no installation needed, just start it and let
**Settings → Tools → Set up…** download yt-dlp, ffmpeg and Deno. Windows 10 or 11, 64-bit.
Videolader is free of charge; this page is its only official source.

**Check the download (optional):** in PowerShell, `Get-FileHash Videolader.exe` must show the value from
`Videolader.exe.sha256`.

**Windows warning:** on the first start Windows may show “Windows protected your PC”. Click “More info” →
“Run anyway”. (Deutsch: „Weitere Informationen“ → „Trotzdem ausführen“. Русский: «Подробнее» →
«Выполнить в любом случае».)
'@

# UTF-8 without BOM, so GitHub shows the text unchanged.
[System.IO.File]::WriteAllText($OutFile, $body + "`n" + $footer + "`n", (New-Object System.Text.UTF8Encoding($false)))
