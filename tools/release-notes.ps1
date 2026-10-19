# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Extracts one version's section from CHANGELOG.md as the GitHub release body.

.DESCRIPTION
    docs/13_CI_CD.md §release.yml: the release body is the CHANGELOG section for the
    tag being released. CHANGELOG.md's own header said for five weeks that a missing
    section "will mean an empty release note"; this script makes a missing section a
    RED release instead, because a release published with no notes is the thing the
    ledger exists to prevent and nobody reads an empty body as a defect.

    The section is everything from the `## [x.y.z]` heading to the next `## ` heading
    (or the end of the file), heading excluded, trimmed. The heading may carry a date
    (`## [0.1.0] - 2026-09-20`, Keep a Changelog's form) or not. An EMPTY section is
    red too — a heading that was moved without its entries is the same defect.

    tools/changelog-check.ps1 asserts a pull request touched the ledger; it does not
    look at sections and this script does not look at diffs. Two questions, two tools.

    WHERE THE SECTION IS (beta.24, owner decision D66). Released sections live one per
    file, `docs/changelog/<version>.md`, each holding exactly its own `## [x.y.z] - date`
    section; CHANGELOG.md keeps `[Unreleased]` and the index. Without -Changelog this
    script reads the version's own file when it exists and CHANGELOG.md otherwise, so a
    tag cut before the move still finds its section where it was.

.PARAMETER Version
    The version whose section to extract, without the leading `v` (the workflow strips it).

.PARAMETER Changelog
    The file to read. Default: the repository's `docs/changelog/<Version>.md` when it
    exists, else its CHANGELOG.md.

.PARAMETER OutFile
    Where to write the body. Written to stdout when omitted.

.PARAMETER SelfTest
    Runs the fixture cases below and exits 0 only if every one behaves; build.ps1
    runs this on every gate so the script is exercised before a tag depends on it.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Changelog,
    [string]$OutFile,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseSection {
    param([string]$Text, [string]$Ver)

    $lines = $Text -split "`r?`n"
    $escaped = [regex]::Escape($Ver)
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^## \[$escaped\](\s|$)") { $start = $i; break }
    }
    if ($start -lt 0) { return $null }

    $body = [System.Collections.Generic.List[string]]::new()
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## ') { break }
        $body.Add($lines[$i])
    }
    return (($body -join "`n").Trim())
}

# The file a version's notes are read from (D66): its own file under docs/changelog/ when there is one, else CHANGELOG.md.
function Resolve-ChangelogFile {
    param([string]$Root, [string]$Ver)

    $own = Join-Path $Root (Join-Path 'docs/changelog' "$Ver.md")
    if ($Ver -and (Test-Path -LiteralPath $own -PathType Leaf)) { return $own }
    return (Join-Path $Root 'CHANGELOG.md')
}

if ($SelfTest) {
    $fixture = @"
# Changelog

## [Unreleased]

### Added

- something pending

## [0.2.0] - 2026-10-01

### Added

- the second thing

### Fixed

- a fix

## [0.1.0]

### Added

- the first thing
"@
    $cases = @(
        @{ Name = 'a dated heading yields its body only'; Ver = '0.2.0'; Expect = "### Added`n`n- the second thing`n`n### Fixed`n`n- a fix" },
        @{ Name = 'an undated last section runs to the end of file'; Ver = '0.1.0'; Expect = "### Added`n`n- the first thing" },
        @{ Name = 'a missing section is null'; Ver = '0.3.0'; Expect = $null },
        @{ Name = 'a prefix does not match (0.1.0 is not 0.1.0-beta.1)'; Ver = '0.1.0-beta.1'; Expect = $null },
        @{ Name = 'an empty section is empty, not the next one'; Ver = 'x'; Text = "## [x]`n`n## [y]`n- y"; Expect = '' }
    )
    $failed = 0
    foreach ($c in $cases) {
        $text = if ($c.ContainsKey('Text')) { $c.Text } else { $fixture }
        $got = Get-ReleaseSection -Text $text -Ver $c.Ver
        $ok = if ($null -eq $c.Expect) { $null -eq $got } else { $got -eq $c.Expect }
        Write-Host ("  {0,-4} {1}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
        if (-not $ok) { $failed++ }
    }

    # Where the notes are read from (D66): the version's own file first, CHANGELOG.md when it has none.
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ("release-notes-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/changelog') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $root 'CHANGELOG.md') -Value "## [Unreleased]`n`n## [0.1.0]`n`n- in the root" -NoNewline
        Set-Content -LiteralPath (Join-Path $root 'docs/changelog/0.2.0.md') -Value "<!-- moved -->`n`n## [0.2.0] - 2026-10-19`n`n- in its own file" -NoNewline
        $fileCases = @(
            @{ Name = 'a version with its own file is read from it'; Ver = '0.2.0'; Expect = '- in its own file' },
            @{ Name = 'a version without one is read from CHANGELOG.md'; Ver = '0.1.0'; Expect = '- in the root' },
            @{ Name = 'a prefix of a file name is not that file'; Ver = '0.2'; Expect = $null }
        )
        foreach ($c in $fileCases) {
            $file = Resolve-ChangelogFile -Root $root -Ver $c.Ver
            $got = Get-ReleaseSection -Text (Get-Content -LiteralPath $file -Raw) -Ver $c.Ver
            $ok = if ($null -eq $c.Expect) { $null -eq $got } else { $got -eq $c.Expect }
            Write-Host ("  {0,-4} {1}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
            if (-not $ok) { $failed++ }
        }
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($failed -gt 0) { Write-Host "release-notes self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host 'release-notes self-test OK' -ForegroundColor Green
    exit 0
}

if (-not $Version) { throw '-Version is required (or -SelfTest)' }
if (-not $Changelog) { $Changelog = Resolve-ChangelogFile -Root (Split-Path $PSScriptRoot -Parent) -Ver $Version }
if (-not (Test-Path $Changelog)) { throw "no changelog at $Changelog" }

$section = Get-ReleaseSection -Text (Get-Content $Changelog -Raw) -Ver $Version
if ($null -eq $section) {
    Write-Host "RELEASE NOTES FAILED: $Changelog has no '## [$Version]' section — move the [Unreleased] entries of CHANGELOG.md into docs/changelog/$Version.md under that heading (and add its row to CHANGELOG.md's Releases table) before tagging" -ForegroundColor Red
    exit 1
}
if ($section.Length -eq 0) {
    Write-Host "RELEASE NOTES FAILED: the '## [$Version]' section is empty" -ForegroundColor Red
    exit 1
}

if ($OutFile) {
    [System.IO.File]::WriteAllText($OutFile, $section + "`n", [System.Text.UTF8Encoding]::new($false))
    Write-Host "release notes for $Version written to $OutFile ($($section.Length) chars)" -ForegroundColor Green
}
else {
    Write-Output $section
}
exit 0
