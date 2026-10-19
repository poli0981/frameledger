# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    The long documents split in beta.24 (owner decision D66) stay split, and each index and the files it names agree
    in both directions.

.DESCRIPTION
    beta.24 moved the released sections of CHANGELOG.md to docs/changelog/<version>.md (and the ledger kept before
    the first tag to docs/changelog/ledger-before-0.1.0-beta.1/), and the history of docs/HANDOFF.md to
    docs/handoff/. Each original path stayed, as the index of its parts, because code and documents cite the old
    sections by name. An index that names a file that is gone, or a part that no index names, is a section nobody
    can find from the citation — and nothing else would notice. So this gate holds:

      CHANGELOG   - no version heading is left in CHANGELOG.md beside `## [Unreleased]`;
                  - every docs/changelog/<file>.md holds ONE `## ` heading, `## [<file name>] - YYYY-MM-DD`
                    (tools/release-notes.ps1 reads the section from that heading to the next `## `);
                  - no relative Markdown link in those files: the App renders release notes with the document path
                    CHANGELOG.md, so a link relative to docs/changelog/ would resolve wrongly there;
                  - CHANGELOG.md's links to docs/changelog/ and the files there name each other, both ways.
      HANDOFF     - docs/HANDOFF.md's links into handoff/ and the files under docs/handoff/ name each other, both ways.
      OPEN QUESTIONS, SPIKE NOTES (beta.24 PR-5) - docs/20_OPEN_QUESTIONS.md and docs/open-questions/ (the closed
                    entries), docs/spike-notes.md and docs/spike-notes/ (one file per section) likewise.

    -SelfTest proves every rule in both directions on a temporary tree.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:VersionHeading = '^## \[(?<v>[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?)\] - (?<d>[0-9]{4}-[0-9]{2}-[0-9]{2})$'

# Relative Markdown link targets in $Text: not http(s), not mailto, not an anchor in the same page.
function Get-RelativeLink([string]$Text) {
    $out = @()
    foreach ($m in [regex]::Matches($Text, '\]\((?<t>[^)\s]+)\)')) {
        $t = $m.Groups['t'].Value
        if ($t -notmatch '^(https?:|mailto:|#)') { $out += $t }
    }
    return , $out
}

# Every link target in $Text that starts with $Prefix, without its #anchor.
function Get-LinkedPath([string]$Text, [string]$Prefix) {
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($m in [regex]::Matches($Text, '\]\((?<t>[^)\s#]+)(#[^)\s]*)?\)')) {
        $t = $m.Groups['t'].Value
        if ($t.StartsWith($Prefix, [StringComparison]::Ordinal) -and -not $out.Contains($t)) { $out.Add($t) }
    }
    return , $out.ToArray()
}

# The relative paths of the .md files under $Dir (forward slashes), recursively.
function Get-MarkdownFile([string]$Root, [string]$Dir) {
    $full = Join-Path $Root $Dir
    if (-not (Test-Path -LiteralPath $full -PathType Container)) { return , @() }
    $files = Get-ChildItem -LiteralPath $full -Recurse -File -Filter '*.md' |
        ForEach-Object { [System.IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/') }
    return , @($files | Sort-Object)
}

function Test-Changelog([string]$Root) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $rootFile = Join-Path $Root 'CHANGELOG.md'
    $text = Get-Content -LiteralPath $rootFile -Raw
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^## \[' -and $line -ne '## [Unreleased]') {
            $problems.Add("CHANGELOG.md still holds a released section: '$line' (it belongs in docs/changelog/)")
        }
    }

    $parts = Get-MarkdownFile -Root $Root -Dir 'docs/changelog'
    foreach ($part in $parts) {
        $body = Get-Content -LiteralPath (Join-Path $Root $part) -Raw
        foreach ($link in (Get-RelativeLink $body)) {
            $problems.Add("$part has a relative link '$link' (release notes render as CHANGELOG.md; use a backticked path)")
        }
        if ($part -match '^docs/changelog/(?<name>[^/]+)\.md$') {
            $name = $Matches['name']
            $headings = @(($body -split "`r?`n") | Where-Object { $_ -match '^## ' })
            if ($headings.Count -ne 1) {
                $problems.Add("$part holds $($headings.Count) '## ' headings; a release file holds exactly its own")
            }
            elseif (-not ($headings[0] -match $script:VersionHeading -and $Matches['v'] -eq $name)) {
                $problems.Add("$part opens with '$($headings[0])', not '## [$name] - YYYY-MM-DD'")
            }
        }
    }

    $linked = Get-LinkedPath -Text $text -Prefix 'docs/changelog/'
    foreach ($link in $linked) {
        if ($parts -notcontains $link) { $problems.Add("CHANGELOG.md links $link, which does not exist") }
    }
    foreach ($part in $parts) {
        if ($linked -notcontains $part) { $problems.Add("$part is not linked from CHANGELOG.md") }
    }
    return , $problems.ToArray()
}

# An index under docs/ and the folder of its parts (links relative to docs/) name each other, both ways.
function Test-IndexedFolder([string]$Root, [string]$Index, [string]$Folder) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $indexPath = Join-Path $Root "docs/$Index"
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) { return , @("docs/$Index is missing") }
    $text = Get-Content -LiteralPath $indexPath -Raw
    $parts = @(Get-MarkdownFile -Root $Root -Dir "docs/$Folder" | ForEach-Object { $_ -replace '^docs/', '' })
    $linked = Get-LinkedPath -Text $text -Prefix "$Folder/"
    foreach ($link in $linked) {
        if ($parts -notcontains $link) { $problems.Add("docs/$Index links $link, which does not exist") }
    }
    foreach ($part in $parts) {
        if ($linked -notcontains $part) { $problems.Add("docs/$part is not linked from docs/$Index") }
    }
    return , $problems.ToArray()
}

function Invoke-Check([string]$Root) {
    $all = @()
    $all += Test-Changelog $Root
    $all += Test-IndexedFolder $Root 'HANDOFF.md' 'handoff'
    $all += Test-IndexedFolder $Root '20_OPEN_QUESTIONS.md' 'open-questions'
    $all += Test-IndexedFolder $Root 'spike-notes.md' 'spike-notes'
    return , $all
}

if ($SelfTest) {
    $failed = 0
    function New-Tree {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) ("docs-split-selftest-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/changelog/ledger-before-0.1.0-beta.1') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/handoff/history') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $root 'CHANGELOG.md') -NoNewline -Value (
            "# Changelog`n`n## [Unreleased]`n`n## Releases`n`n| Version | Date | Notes |`n|---|---|---|`n" +
            "| ``0.2.0`` | 2026-10-19 | [``docs/changelog/0.2.0.md``](docs/changelog/0.2.0.md) |`n`n" +
            "- [``docs/changelog/ledger-before-0.1.0-beta.1/part-1.md``](docs/changelog/ledger-before-0.1.0-beta.1/part-1.md)`n")
        Set-Content -LiteralPath (Join-Path $root 'docs/changelog/0.2.0.md') -NoNewline -Value "<!-- moved -->`n`n## [0.2.0] - 2026-10-19`n`n- a change in ``src/x.cs```n"
        Set-Content -LiteralPath (Join-Path $root 'docs/changelog/ledger-before-0.1.0-beta.1/part-1.md') -NoNewline -Value "## Ledger before 0.1.0-beta.1`n`n### Added`n"
        Set-Content -LiteralPath (Join-Path $root 'docs/HANDOFF.md') -NoNewline -Value (
            "# HANDOFF`n`n| Cited as | Now in |`n|---|---|`n| ``§P2`` | [``handoff/history/p2.md``](handoff/history/p2.md) |`n" +
            "| traps | [``handoff/traps.md``](handoff/traps.md) |`n")
        Set-Content -LiteralPath (Join-Path $root 'docs/handoff/history/p2.md') -NoNewline -Value "# HANDOFF history - P2`n"
        Set-Content -LiteralPath (Join-Path $root 'docs/handoff/traps.md') -NoNewline -Value "# traps`n"
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/open-questions') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/spike-notes') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $root 'docs/20_OPEN_QUESTIONS.md') -NoNewline -Value "| S2 | [``open-questions/closed-s.md``](open-questions/closed-s.md) |`n"
        Set-Content -LiteralPath (Join-Path $root 'docs/open-questions/closed-s.md') -NoNewline -Value "# closed`n"
        Set-Content -LiteralPath (Join-Path $root 'docs/spike-notes.md') -NoNewline -Value "| §0 | [``spike-notes/00-licence-checks.md``](spike-notes/00-licence-checks.md) |`n"
        Set-Content -LiteralPath (Join-Path $root 'docs/spike-notes/00-licence-checks.md') -NoNewline -Value "## 0`n"
        return $root
    }
    $cases = @(
        @{ Name = 'a tree as split is clean'; Break = { param($r) }; Expect = 0 },
        @{ Name = 'a released section left in CHANGELOG.md is red'; Expect = 1; Break = { param($r) Add-Content -LiteralPath (Join-Path $r 'CHANGELOG.md') -Value "`n## [0.1.0] - 2026-09-16`n" } },
        @{ Name = 'a release file whose heading is not its name is red'; Expect = 1; Break = { param($r) Set-Content -LiteralPath (Join-Path $r 'docs/changelog/0.2.0.md') -Value "## [0.3.0] - 2026-10-19`n" } },
        @{ Name = 'a release file with a second section is red'; Expect = 1; Break = { param($r) Add-Content -LiteralPath (Join-Path $r 'docs/changelog/0.2.0.md') -Value "`n## [0.1.0] - 2026-09-16`n" } },
        @{ Name = 'a relative link in a release file is red'; Expect = 1; Break = { param($r) Add-Content -LiteralPath (Join-Path $r 'docs/changelog/0.2.0.md') -Value "`n- see [the guide](../../guide/README.md)`n" } },
        @{ Name = 'a release file nothing links is red'; Expect = 1; Break = { param($r) Set-Content -LiteralPath (Join-Path $r 'docs/changelog/0.1.0.md') -Value "## [0.1.0] - 2026-09-16`n" } },
        @{ Name = 'a link to a release file that is gone is red'; Expect = 1; Break = { param($r) Remove-Item -LiteralPath (Join-Path $r 'docs/changelog/ledger-before-0.1.0-beta.1/part-1.md') } },
        @{ Name = 'a handoff part nothing links is red'; Expect = 1; Break = { param($r) Set-Content -LiteralPath (Join-Path $r 'docs/handoff/history/p3.md') -Value "# P3`n" } },
        @{ Name = 'a handoff link to a part that is gone is red'; Expect = 1; Break = { param($r) Remove-Item -LiteralPath (Join-Path $r 'docs/handoff/traps.md') } },
        @{ Name = 'a closed-entries file nothing links is red'; Expect = 1; Break = { param($r) Set-Content -LiteralPath (Join-Path $r 'docs/open-questions/closed-h.md') -Value "# closed H`n" } },
        @{ Name = 'a spike-notes section that is gone is red'; Expect = 1; Break = { param($r) Remove-Item -LiteralPath (Join-Path $r 'docs/spike-notes/00-licence-checks.md') } }
    )
    foreach ($c in $cases) {
        $root = New-Tree
        try {
            & $c.Break $root
            $problems = Invoke-Check $root
            $ok = $problems.Count -eq $c.Expect
            Write-Host ("  {0,-4} {1}{2}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name, ($(if (-not $ok) { " -> $($problems -join '; ')" } else { '' }))) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
            if (-not $ok) { $failed++ }
        }
        finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if ($failed -gt 0) { Write-Host "docs-split-check self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host 'docs-split-check self-test OK' -ForegroundColor Green
    exit 0
}

$problems = Invoke-Check $RepoRoot
if ($problems.Count -gt 0) {
    Write-Host 'DOCS SPLIT CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}
Write-Host 'docs-split-check OK - every split document and its index agree' -ForegroundColor Green
exit 0
