# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the GitHub Wiki from the repository - wiki/ (Home, the sidebar, the developer pages), guide/ (the user
    guide, which is the wiki's End-user part) and LIMITATIONS.md - and refuses a wiki with a link that leads nowhere.

.DESCRIPTION
    D53/D54 (the owner, 2026-10-07): the wiki's source lives in the repository and the Wiki tab is a copy of it, written
    by .github/workflows/wiki.yml when a release is published. The user guide is the End-user part: one source, shown
    in the App (Help > User guide) and on the wiki.

    Assembling:
      - wiki/*.md keeps its file name, which GitHub turns into the page's title (Build-and-test.md: "Build and test").
      - A guide page becomes the page its first heading names (guide/README.md is User-guide, LIMITATIONS.md is
        Limitations). Every page's first heading is dropped: the wiki prints the page's name above it.
      - A link to one of those pages becomes the page's name; a link to any other file or folder of the repository
        becomes its address on GitHub at -Ref, the release being published; anchors are kept.
      - _Footer.md is written here, naming -Ref and where to edit.

    Refused, each with a line, exit 1:
      - a link to a file or folder that does not exist, or to a section no heading of its page produces;
      - two sources that give one page name;
      - a page that neither Home nor the sidebar leads to;
      - a sidebar that lists the guide's pages in another order than guide/README.md does;
      - a path in backticks in a wiki/ page that names nothing in the repository;
      - a version number written in a wiki/ page (the footer is where the wiki says which release it shows).

    -Check assembles into a temporary folder and only reports. -SelfTest proves every refusal, both ways, on copies.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$Out,
    [string]$Ref = 'main',
    [switch]$Check,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoUrl = 'https://github.com/poli0981/frameledger'
$special = @{ 'guide/README.md' = 'User-guide'; 'LIMITATIONS.md' = 'Limitations' }
$pathPrefixes = @('src/', 'docs/', 'tools/', 'tests/', 'guide/', 'legal/', 'wiki/', 'rules/', '.github/')
$rootFiles = @('README.md', 'CLAUDE.md', 'CHANGELOG.md', 'LIMITATIONS.md', 'CONTRIBUTING.md', 'FORKING.md', 'SECURITY.md',
    'CODE_OF_CONDUCT.md', 'NOTICE', 'LICENSE', 'VERSION', 'build.ps1', 'global.json', 'FrameLedger.slnx', 'Directory.Build.props')

if ($Ref -notmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$') { throw "-Ref '$Ref' is not a branch, tag or commit name" }

# The anchor GitHub gives a heading: lower-case, letters, digits, '-' and '_' kept, a space a '-', the rest dropped
# (MarkdownLinks.Slug in the App follows the same rule, so the guide's anchors work in both places).
function Get-Slug([string]$Heading) {
    $sb = [System.Text.StringBuilder]::new()
    foreach ($c in $Heading.Trim().ToLowerInvariant().ToCharArray()) {
        if ([char]::IsLetterOrDigit($c) -or $c -eq '-' -or $c -eq '_') { [void]$sb.Append($c) }
        elseif ($c -eq ' ') { [void]$sb.Append('-') }
    }
    return $sb.ToString()
}

# Lines outside fenced code blocks, each as @{ Index; Text }.
function Get-ProseLines([string[]]$Lines) {
    $inFence = $false
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^\s*(```|~~~)') { $inFence = -not $inFence; continue }
        if (-not $inFence) { [pscustomobject]@{ Index = $i; Text = $Lines[$i] } }
    }
}

function Get-Anchors([string]$Text) {
    $anchors = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in Get-ProseLines (($Text -replace "`r`n", "`n") -split "`n")) {
        if ($line.Text -match '^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$') {
            $heading = [regex]::Replace($Matches[1], '\[([^\]]*)\]\([^)]*\)', '$1') -replace '[`*]', ''
            $slug = Get-Slug $heading
            if ($slug.Length -eq 0) { continue }
            $unique = $slug
            for ($n = 1; $anchors.Contains($unique); $n++) { $unique = "$slug-$n" }
            [void]$anchors.Add($unique)
        }
    }
    return , $anchors
}

function Get-PageName([string]$Path, [string]$Text) {
    if ($special.ContainsKey($Path)) { return $special[$Path] }
    if ($Path.StartsWith('wiki/', [StringComparison]::Ordinal)) { return [IO.Path]::GetFileNameWithoutExtension($Path) }
    $h1 = @(($Text -replace "`r`n", "`n") -split "`n" | Where-Object { $_ -match '^# \S' }) | Select-Object -First 1
    if (-not $h1) { throw "$Path has no first heading to name its wiki page" }
    return (($h1.Substring(2).Trim() -replace '[^A-Za-z0-9]+', '-').Trim('-'))
}

function Get-Sources([string]$SourceRoot) {
    $sources = [System.Collections.Generic.List[object]]::new()
    $add = {
        param([string]$Path, [string]$File)
        $text = [IO.File]::ReadAllText($File)
        $sources.Add([pscustomobject]@{ Path = $Path; Text = $text; Name = (Get-PageName $Path $text); FromWiki = $Path.StartsWith('wiki/', [StringComparison]::Ordinal) })
    }
    foreach ($f in Get-ChildItem (Join-Path $SourceRoot 'wiki') -Filter *.md -File | Sort-Object Name) { & $add "wiki/$($f.Name)" $f.FullName }
    foreach ($f in Get-ChildItem (Join-Path $SourceRoot 'guide') -Filter *.md -File | Sort-Object Name) { & $add "guide/$($f.Name)" $f.FullName }
    & $add 'LIMITATIONS.md' (Join-Path $SourceRoot 'LIMITATIONS.md')
    return , $sources
}

# A link's target as a path from the repository root, or $null when it climbs out of it.
function Resolve-RepoPath([string]$From, [string]$Link) {
    $parts = [System.Collections.Generic.List[string]]::new()
    $dir = [IO.Path]::GetDirectoryName($From) -replace '\\', '/'
    if ($dir) { foreach ($s in $dir -split '/') { $parts.Add($s) } }
    foreach ($seg in ($Link -split '/')) {
        if ($seg -eq '' -or $seg -eq '.') { continue }
        if ($seg -eq '..') {
            if ($parts.Count -eq 0) { return $null }
            $parts.RemoveAt($parts.Count - 1)
            continue
        }
        $parts.Add($seg)
    }
    return ($parts -join '/')
}

# Every inline link of a page outside code: @{ Start; Length; Href }, in order.
function Get-Links([string]$Text) {
    $lines = $Text -split "`n"
    $offsets = [int[]]::new($lines.Count)
    $at = 0
    for ($i = 0; $i -lt $lines.Count; $i++) { $offsets[$i] = $at; $at += $lines[$i].Length + 1 }
    foreach ($line in Get-ProseLines $lines) {
        foreach ($m in [regex]::Matches($line.Text, '(?<!!)\]\((?<href>[^)\s]+)(?:\s+"[^"]*")?\)')) {
            $g = $m.Groups['href']
            [pscustomobject]@{ Start = $offsets[$line.Index] + $g.Index; Length = $g.Length; Href = $g.Value }
        }
    }
}

# Assembles the wiki in memory: @{ Pages = name -> text; Problems = string[] }.
function Invoke-Assembly([string]$SourceRoot, [string]$Root, [string]$GitRef) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $sources = Get-Sources $SourceRoot
    $byPath = @{}
    $byName = @{}
    foreach ($s in $sources) {
        $byPath[$s.Path] = $s
        if ($byName.ContainsKey($s.Name)) { $problems.Add("$($s.Path) and $($byName[$s.Name].Path) both make the page '$($s.Name)'") }
        else { $byName[$s.Name] = $s }
    }
    foreach ($req in 'Home', '_Sidebar') {
        if (-not $byName.ContainsKey($req)) { $problems.Add("wiki/$req.md is missing") }
    }

    $anchorCache = @{}
    $anchorsOf = {
        param([string]$Path, [string]$Text)
        if (-not $anchorCache.ContainsKey($Path)) { $anchorCache[$Path] = Get-Anchors $Text }
        return , $anchorCache[$Path]
    }

    $pages = [ordered]@{}
    $edges = @{}
    foreach ($s in $sources) {
        $text = $s.Text -replace "`r`n", "`n"
        $targets = [System.Collections.Generic.List[string]]::new()
        $links = @(Get-Links $text)
        $sb = [System.Text.StringBuilder]::new($text)
        for ($k = $links.Count - 1; $k -ge 0; $k--) {
            $link = $links[$k]
            $href = $link.Href
            if ($href -match '^(https?:|mailto:)') { continue }
            $hash = $href.IndexOf('#')
            $anchor = if ($hash -ge 0) { $href.Substring($hash + 1) } else { $null }
            $pathPart = if ($hash -ge 0) { $href.Substring(0, $hash) } else { $href }
            $new = $null
            if ($pathPart -eq '') {
                if (-not $anchor -or -not (& $anchorsOf $s.Path $s.Text).Contains($anchor.ToLowerInvariant())) { $problems.Add("$($s.Path): '$href' names no section of the page") }
                continue
            }
            $isDir = $pathPart.EndsWith('/')
            $target = Resolve-RepoPath $s.Path ([Uri]::UnescapeDataString($pathPart))
            if ($null -eq $target -or $target -eq '') { $problems.Add("$($s.Path): '$href' leaves the repository"); continue }
            if ($byPath.ContainsKey($target)) {
                $page = $byPath[$target]
                if ($anchor -and -not (& $anchorsOf $page.Path $page.Text).Contains($anchor.ToLowerInvariant())) { $problems.Add("$($s.Path): '$href' names no section of $target") }
                $new = $page.Name + $(if ($anchor) { "#$anchor" } else { '' })
                $targets.Add($page.Name)
            }
            else {
                $full = Join-Path $Root ($target -replace '/', [IO.Path]::DirectorySeparatorChar)
                $exists = if ($isDir) { Test-Path -LiteralPath $full -PathType Container } else { Test-Path -LiteralPath $full }
                if (-not $exists) { $problems.Add("$($s.Path): '$href' leads to $target, which does not exist"); continue }
                $folder = Test-Path -LiteralPath $full -PathType Container
                if ($anchor -and ($folder -or -not $target.EndsWith('.md') -or -not (& $anchorsOf $target ([IO.File]::ReadAllText($full))).Contains($anchor.ToLowerInvariant()))) {
                    $problems.Add("$($s.Path): '$href' names no section of $target")
                }
                $kind = if ($folder) { 'tree' } else { 'blob' }
                $new = "$repoUrl/$kind/$GitRef/$($target.TrimEnd('/'))" + $(if ($anchor) { "#$anchor" } else { '' })
            }
            [void]$sb.Remove($link.Start, $link.Length).Insert($link.Start, $new)
        }

        # The wiki prints the page's name as its title; the source's own first heading - kept for reading it in the
        # repository and, for a guide page, in the App - would repeat it.
        $converted = [regex]::Replace($sb.ToString(), '\A(?:<!--.*?-->\s*)*# [^\n]*\n(\s*\n)?', '', [Text.RegularExpressions.RegexOptions]::Singleline)
        if ($s.FromWiki) { Test-WikiSource $s $Root $problems }

        $pages[$s.Name] = $converted
        $edges[$s.Name] = $targets
    }

    Test-Reachable $edges $byName $problems
    Test-SidebarOrder $byPath $problems
    $pages['_Footer'] = "This wiki is generated from the repository at [``$GitRef``]($repoUrl/tree/$GitRef). Edit " +
        "[``wiki/``]($repoUrl/tree/main/wiki) or [``guide/``]($repoUrl/tree/main/guide) there: a change made here is " +
        "overwritten when the next release is published.`n"
    return [pscustomobject]@{ Pages = $pages; Problems = $problems.ToArray() }
}

# A wiki/ page names only paths that exist, and no version number.
function Test-WikiSource($Source, [string]$Root, $Problems) {
    foreach ($line in Get-ProseLines (($Source.Text -replace "`r`n", "`n") -split "`n")) {
        foreach ($m in [regex]::Matches($line.Text, '`([^`\s]+)`')) {
            $candidate = $m.Groups[1].Value.TrimEnd('.', ',', ';', ':')
            $isPath = ($pathPrefixes | Where-Object { $candidate.StartsWith($_, [StringComparison]::Ordinal) }) -or ($rootFiles -contains $candidate)
            if (-not $isPath -or $candidate -match '[*<>{}|?]') { continue }
            $full = Join-Path $Root ($candidate.TrimEnd('/') -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $full)) { $Problems.Add("$($Source.Path): ``$candidate`` names nothing in the repository") }
        }
        if ($line.Text -match '\b\d+\.\d+\.\d+-(?:beta|rc|alpha)\.\d+\b') {
            $Problems.Add("$($Source.Path): '$($Matches[0])' - a wiki page names no version; the footer says which release it shows")
        }
    }
}

function Test-Reachable($Edges, $ByName, $Problems) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $queue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($start in 'Home', '_Sidebar') { if ($ByName.ContainsKey($start) -and $seen.Add($start)) { $queue.Enqueue($start) } }
    while ($queue.Count -gt 0) {
        foreach ($next in $Edges[$queue.Dequeue()]) { if ($seen.Add($next)) { $queue.Enqueue($next) } }
    }
    foreach ($name in $ByName.Keys | Sort-Object) {
        if (-not $seen.Contains($name)) { $Problems.Add("$($ByName[$name].Path): no link from Home or the sidebar leads to '$name'") }
    }
}

# The sidebar lists the guide's pages in the order of the guide's own contents.
function Test-SidebarOrder($ByPath, $Problems) {
    if (-not $ByPath.ContainsKey('wiki/_Sidebar.md') -or -not $ByPath.ContainsKey('guide/README.md')) { return }
    $order = {
        param($Source)
        $paths = foreach ($l in Get-Links ($Source.Text -replace "`r`n", "`n")) {
            $hash = $l.Href.IndexOf('#')
            $p = Resolve-RepoPath $Source.Path ([Uri]::UnescapeDataString($(if ($hash -ge 0) { $l.Href.Substring(0, $hash) } else { $l.Href })))
            if ($p -and $p.StartsWith('guide/', [StringComparison]::Ordinal) -and $p -ne 'guide/README.md') { $p }
        }
        return @($paths | Select-Object -Unique)
    }
    $contents = & $order $ByPath['guide/README.md']
    $sidebar = & $order $ByPath['wiki/_Sidebar.md']
    if (($contents -join '|') -ne ($sidebar -join '|')) {
        $Problems.Add("wiki/_Sidebar.md lists the guide as [$($sidebar -join ', ')], guide/README.md as [$($contents -join ', ')]")
    }
}

function Write-Wiki($Pages, [string]$Folder) {
    New-Item -ItemType Directory -Path $Folder -Force | Out-Null
    Get-ChildItem -LiteralPath $Folder -Force | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    foreach ($name in $Pages.Keys) {
        [IO.File]::WriteAllText((Join-Path $Folder "$name.md"), ($Pages[$name] -replace "`r`n", "`n"), $utf8)
    }
}

if ($SelfTest) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('fl-wiki-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        Copy-Item (Join-Path $RepoRoot 'wiki') (Join-Path $tmp 'wiki') -Recurse
        Copy-Item (Join-Path $RepoRoot 'guide') (Join-Path $tmp 'guide') -Recurse
        Copy-Item (Join-Path $RepoRoot 'LIMITATIONS.md') $tmp
        $cases = 0
        $expect = {
            param([string]$What, [string]$Pattern)
            $p = @((Invoke-Assembly $tmp $RepoRoot $Ref).Problems)
            if ($Pattern -eq '') {
                if ($p.Count -ne 0) { throw "self-test: $What should pass, got: $($p -join '; ')" }
            }
            elseif ($p.Count -ne 1 -or $p[0] -notmatch $Pattern) { throw "self-test: $What should fail once matching '$Pattern', got: $($p -join '; ')" }
            $script:cases++
        }
        $mutate = {
            param([string]$Rel, [scriptblock]$Change, [string]$What, [string]$Pattern)
            $file = Join-Path $tmp $Rel
            $existed = Test-Path $file
            $orig = if ($existed) { [IO.File]::ReadAllText($file) } else { $null }
            [IO.File]::WriteAllText($file, (& $Change $orig))
            try { & $expect $What $Pattern }
            finally { if ($existed) { [IO.File]::WriteAllText($file, $orig) } else { Remove-Item $file } }
        }

        & $expect 'the shipped sources' ''
        & $mutate 'wiki/Home.md' { param($t) $t + "`n[gone](../docs/NO_SUCH_FILE.md)`n" } 'a link to a missing file' 'NO_SUCH_FILE.*does not exist'
        & $mutate 'wiki/Home.md' { param($t) $t + "`n[gone](../guide/01-install.md#no-such-section)`n" } 'a link to a missing section' 'no-such-section.*names no section'
        & $mutate 'wiki/Home.md' { param($t) $t + "`n[out](../../outside.md)`n" } 'a link out of the repository' 'leaves the repository'
        & $mutate 'wiki/Limitations.md' { param($t) "Same name as LIMITATIONS.md.`n" } 'two sources for one page' "both make the page 'Limitations'"
        & $mutate 'wiki/Orphan.md' { param($t) "Nothing links here.`n" } 'a page nothing leads to' "leads to 'Orphan'"
        & $mutate 'wiki/_Sidebar.md' { param($t) ($t -replace '(?m)^- \[Questions and answers\]\(\.\./guide/06-faq\.md\)\r?\n', '') + "- [Questions and answers](../guide/06-faq.md)`n" } 'a sidebar out of the guide''s order' 'lists the guide as'
        & $mutate 'wiki/Home.md' { param($t) $t + "`nSee ``src/FrameLedger.App/NoSuchFile.cs``.`n" } 'a backticked path that names nothing' 'NoSuchFile\.cs.*names nothing'
        & $mutate 'wiki/Home.md' { param($t) $t + "`nSince 0.1.0-beta.99.`n" } 'a version number in a page' 'names no version'
        Write-Host "wiki-build self-test OK - $cases cases, both directions"
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
    exit 0
}

$result = Invoke-Assembly $RepoRoot $RepoRoot $Ref
if ($result.Problems.Count -ne 0) {
    Write-Host 'WIKI CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $result.Problems) { Write-Host "  $p" -ForegroundColor Red }
    Write-Host '  The wiki is built from wiki/, guide/ and LIMITATIONS.md (docs/13_CI_CD.md, wiki.yml).' -ForegroundColor Red
    exit 1
}
if ($Check) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('fl-wiki-check-' + [Guid]::NewGuid().ToString('N'))
    try { Write-Wiki $result.Pages $tmp } finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "wiki-check OK - $($result.Pages.Count) pages, every link leads somewhere"
    exit 0
}
if (-not $Out) { $Out = Join-Path $RepoRoot 'out/wiki' }
Write-Wiki $result.Pages $Out
Write-Host "wiki-build OK - $($result.Pages.Count) pages written to $Out at $Ref"
exit 0
