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

.PARAMETER Version
    The version whose section to extract, without the leading `v` (the workflow strips it).

.PARAMETER Changelog
    Path to CHANGELOG.md. Default: the repository's.

.PARAMETER OutFile
    Where to write the body. Written to stdout when omitted.

.PARAMETER SelfTest
    Runs the fixture cases below and exits 0 only if every one behaves; build.ps1
    runs this on every gate so the script is exercised before a tag depends on it.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Changelog = (Join-Path (Split-Path $PSScriptRoot -Parent) 'CHANGELOG.md'),
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
    if ($failed -gt 0) { Write-Host "release-notes self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host 'release-notes self-test OK' -ForegroundColor Green
    exit 0
}

if (-not $Version) { throw '-Version is required (or -SelfTest)' }
if (-not (Test-Path $Changelog)) { throw "no changelog at $Changelog" }

$section = Get-ReleaseSection -Text (Get-Content $Changelog -Raw) -Ver $Version
if ($null -eq $section) {
    Write-Host "RELEASE NOTES FAILED: CHANGELOG.md has no '## [$Version]' section — add one (move the [Unreleased] entries under it) before tagging" -ForegroundColor Red
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
