# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Asserts every shipped binary carries a populated version block that names
    FrameLedger: the native ones, and since beta.14 the managed ones.

.DESCRIPTION
    docs/19_SAFETY_AND_ANTICHEAT.md §What we will never build: the DLL ships
    with its real filename, real exports, and a populated version block. An
    anti-cheat vendor looking at a module inside their game must be able to tell
    instantly what it is and who wrote it. Being identifiable is the design
    principle, not a side effect — anything that made these fields vaguer,
    absent or randomised would be evasion, which CLAUDE.md rule 3 rejects.

    src/native/FrameLedger.Overlay/CMakeLists.txt carried the comment
    "VERSIONINFO is mandatory and CI fails the build without it" directly above
    a TODO to add it, while no .rc file existed anywhere and nothing checked.
    This script is what makes that sentence true.

    Reads the BUILT BINARY rather than the .rc source: what ships is what
    matters, and a resource that fails to compile in would leave the source
    looking correct.

    THE MANAGED HALF (beta.14, D50). Until beta.14 this read the five native
    binaries and nothing else, and the managed ones carried the SDK's defaults:
    the Agent's CompanyName and ProductName read "FrameLedger.Agent", each
    library's its own assembly name, and no managed binary had a copyright.
    Explorer's Properties > Details and Task Manager show these blocks, and the
    Agent is the process that injects. -ManagedDir names where the shipped
    managed binaries are (build.ps1: the App's and the Agent's build output;
    release.yml: out/app) and -ExpectedProductVersion the version they must
    carry (the VERSION file's in the gate, the tag's in a release) — required,
    because a comparison with nothing passes everything. Every binary one run
    reads must carry the SAME LegalCopyright, so the managed copyright
    (Directory.Build.props) and the native one (FL_COPYRIGHT in
    src/native/CMakeLists.txt) cannot drift apart.

    A managed .exe is the SDK's apphost, which carries a COPY of its assembly's
    block: its OriginalFilename is the assembly's (FrameLedger.dll), and that is
    what is asserted. An apphost the block was never copied into carries the
    host template's block or none, and is red.

    TWO INVOCATIONS, BOTH WIRED IN build.ps1. -SelfTest runs the rules against
    synthetic blocks — ten cases, nine of which must come back RED, each for the
    reason it names — and the live pass reads the binaries.
#>
[CmdletBinding(DefaultParameterSetName = 'Live')]
param(
    [Parameter(ParameterSetName = 'Live')]
    [string]$BuildDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'build/native/x64-release'),

    # P4 PR-5: the one version source (docs/12_BUILD.md §Version). Every binary's
    # FileVersion must be this, with the fourth component the .rc template adds.
    # Default: the repository's VERSION file; an empty string skips the comparison.
    [Parameter(ParameterSetName = 'Live')]
    [string]$ExpectedVersion = $(
        $f = Join-Path (Split-Path $PSScriptRoot -Parent) 'VERSION'
        if (Test-Path $f) { (Get-Content $f -Raw).Trim() } else { '' }
    ),

    # beta.14 (D50): the directories that hold the shipped managed binaries,
    # searched recursively, the newest copy of each name read. None: the native
    # half only, as before beta.14.
    [Parameter(ParameterSetName = 'Live')]
    [string[]]$ManagedDir = @(),

    # beta.14: the ProductVersion every managed binary must carry — the full
    # version, a pre-release's tag included. The managed half is red without one.
    [Parameter(ParameterSetName = 'Live')]
    [string]$ExpectedProductVersion = '',

    # beta.14: the managed half alone, for build.ps1 -SkipNative, which builds no
    # native binary to read.
    [Parameter(ParameterSetName = 'Live')]
    [switch]$ManagedOnly,

    [Parameter(Mandatory, ParameterSetName = 'SelfTest')]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Every binary that can end up inside a process we do not own.
$nativeBinaries = @('FrameLedger.Overlay.dll', 'FrameLedger.VkLayer.dll', 'FrameLedger.Guard.dll', 'FrameLedger.NvapiBridge.dll', 'FrameLedger.ProcessStats.dll')

# beta.14 (D50): the managed binaries out/app ships — both entry points, apphost
# and assembly, and the four libraries they load.
$managedBinaries = @('FrameLedger.exe', 'FrameLedger.dll', 'FrameLedger.Agent.exe', 'FrameLedger.Agent.dll',
    'FrameLedger.Domain.dll', 'FrameLedger.Application.dll', 'FrameLedger.Infrastructure.dll', 'FrameLedger.Shared.dll')

# The rules for ONE block. Every string it emits is a problem; none is a pass.
# $Block carries FileVersionInfo's field names: a real FileVersionInfo in the
# live pass, a [pscustomobject] in the self-test.
function Test-Block {
    param([string]$Name, [ValidateSet('native', 'managed')][string]$Kind, $Block, [string]$ExpectedVersion, [string]$ExpectedProductVersion)

    # A missing resource yields an object whose fields are all null/empty, not
    # an exception. "No version block" and "a version block full of blanks" are
    # the same failure for our purposes: neither identifies the binary. A
    # copyright of one space is what every managed binary carried until beta.14.
    $empty = @('CompanyName', 'ProductName', 'FileDescription', 'FileVersion', 'LegalCopyright' |
            Where-Object { [string]::IsNullOrWhiteSpace($Block.$_) })
    if ($empty.Count -gt 0) {
        "$Name has no usable version block — empty: $($empty -join ', ')"
        return
    }

    # The product name is the one an anti-cheat vendor greps for. A binary that
    # calls itself something else is the beginning of hiding. The company is the
    # same name (FL_COMPANY_NAME, Directory.Build.props).
    foreach ($field in 'CompanyName', 'ProductName') {
        if ($Block.$field -cne 'FrameLedger') {
            "$Name $field is '$($Block.$field)', expected 'FrameLedger' — the binary must name the project it belongs to"
        }
    }

    # ABSENT is the case this check exists for, and it used to be the one case
    # that passed. `if ($vi.OriginalFilename -and ...)` short-circuits on an empty
    # field, so a binary carrying NO OriginalFilename at all — the least
    # identifiable state there is — sailed through a gate whose whole purpose is
    # that our binaries name themselves.
    $apphost = $Kind -eq 'managed' -and $Name.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)
    $original = if ($apphost) { [IO.Path]::ChangeExtension($Name, '.dll') } else { $Name }
    if ([string]::IsNullOrWhiteSpace($Block.OriginalFilename)) {
        "$Name carries no OriginalFilename — an unnamed binary is the state this check exists to prevent"
    }
    elseif ($Block.OriginalFilename -ne $original) {
        if ($apphost) {
            "$Name OriginalFilename is '$($Block.OriginalFilename)', expected '$original' — an apphost carries a copy of its own assembly's block, so any other is not this binary's"
        }
        else {
            "$Name OriginalFilename is '$($Block.OriginalFilename)' — a renamed binary is harder to identify, which is the wrong direction"
        }
    }

    # The version the binary says is the version the repository says (P4 PR-5). A
    # native block reading 0.0.0 beside a managed assembly reading 0.1.0 is two
    # products claiming to be one, and the release workflow would ship it.
    if ($ExpectedVersion -and $Block.FileVersion.Trim() -ne "$ExpectedVersion.0") {
        "$Name FileVersion is '$($Block.FileVersion)', expected '$ExpectedVersion.0' — the VERSION file is the one source (12_BUILD §Version)"
    }

    # The managed ProductVersion is the full version About, Hello and the bug
    # bundle print — a pre-release's tag included (12_BUILD §Version).
    if ($Kind -eq 'managed') {
        if ([string]::IsNullOrWhiteSpace($ExpectedProductVersion)) {
            "$Name has no -ExpectedProductVersion to compare its ProductVersion with — a comparison with nothing passes everything"
        }
        elseif ($Block.ProductVersion -cne $ExpectedProductVersion) {
            "$Name ProductVersion is '$($Block.ProductVersion)', expected '$ExpectedProductVersion'"
        }
    }
}

# One product, one copyright (D50): every block one run read carries the same line.
function Test-OneCopyright {
    param([System.Collections.IDictionary]$Blocks)
    $lines = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new([StringComparer]::Ordinal)
    foreach ($e in $Blocks.GetEnumerator()) {
        $line = [string]$e.Value.LegalCopyright
        if ([string]::IsNullOrWhiteSpace($line)) { continue }    # Test-Block reported it
        if (-not $lines.ContainsKey($line)) { $lines[$line] = [System.Collections.Generic.List[string]]::new() }
        $lines[$line].Add([string]$e.Key)
    }
    if ($lines.Count -gt 1) {
        "the binaries carry $($lines.Count) different copyright lines where one product has one (Directory.Build.props, FL_COPYRIGHT in src/native/CMakeLists.txt): " +
        (@($lines.GetEnumerator() | ForEach-Object { "'$($_.Key)' in $($_.Value -join ', ')" }) -join '; ')
    }
}

if ($SelfTest) {
    $copyright = 'Copyright (C) 2026 poli0981. GPL-3.0-only with additional terms: see NOTICE'
    $script:cases = 0
    $script:red = 0
    $failures = [System.Collections.Generic.List[string]]::new()

    function New-Block([string]$Original, [string]$Description = 'FrameLedger', [string]$ProductVersion = '0.1.0-beta.14') {
        [pscustomobject][ordered]@{
            CompanyName = 'FrameLedger'; ProductName = 'FrameLedger'; FileDescription = $Description; FileVersion = '0.1.0.0'
            ProductVersion = $ProductVersion; OriginalFilename = $Original; LegalCopyright = $copyright
        }
    }

    # A release's shape, each case changing one thing: a native binary, and both
    # entry points' apphost and assembly.
    function New-Tree {
        [ordered]@{
            'native:FrameLedger.Overlay.dll' = New-Block 'FrameLedger.Overlay.dll' 'FrameLedger Overlay' '0.1.0.0'
            'managed:FrameLedger.exe'        = New-Block 'FrameLedger.dll'
            'managed:FrameLedger.dll'        = New-Block 'FrameLedger.dll'
            'managed:FrameLedger.Agent.exe'  = New-Block 'FrameLedger.Agent.dll' 'FrameLedger Agent'
            'managed:FrameLedger.Agent.dll'  = New-Block 'FrameLedger.Agent.dll' 'FrameLedger Agent'
        }
    }

    # RED must be red for the reason the case names: a fixture typo that empties a
    # field would otherwise make every RED case pass for the wrong one.
    function Invoke-Case([string]$Name, [string]$Because, [scriptblock]$Change, [string]$ProductVersion = '0.1.0-beta.14') {
        $script:cases++
        $tree = New-Tree
        & $Change $tree
        $blocks = [ordered]@{}
        $problems = @(foreach ($e in $tree.GetEnumerator()) {
                $kind, $binary = $e.Key -split ':', 2
                Test-Block $binary $kind $e.Value '0.1.0' $ProductVersion
                $blocks[$binary] = $e.Value
            })
        $problems += @(Test-OneCopyright $blocks)
        $expectRed = [bool]$Because
        $isRed = $problems.Count -gt 0
        if ($isRed) { $script:red++ }
        if ($isRed -ne $expectRed) {
            $failures.Add("$Name — expected $(if ($expectRed) { 'RED' } else { 'GREEN' }), got $(if ($isRed) { 'RED' } else { 'GREEN' }): $($problems -join '; ')")
        }
        elseif ($expectRed -and -not ($problems | Where-Object { $_.Contains($Because, [StringComparison]::Ordinal) })) {
            $failures.Add("$Name — RED, but not for '$Because': $($problems -join '; ')")
        }
        else {
            Write-Host "  ok    $Name $(if ($isRed) { 'FAILS' } else { 'passes' })" -ForegroundColor DarkGray
        }
    }

    # GREEN FIRST: every RED case below is satisfied by a check that refuses
    # everything, so one arrangement must pass or the suite proves nothing works.
    Invoke-Case 'a release built as beta.14 builds it' '' { }
    Invoke-Case 'the Agent named as beta.13 named it' "CompanyName is 'FrameLedger.Agent'" {
        param($t)
        foreach ($k in 'managed:FrameLedger.Agent.exe', 'managed:FrameLedger.Agent.dll') {
            $t[$k].CompanyName = 'FrameLedger.Agent'; $t[$k].ProductName = 'FrameLedger.Agent'; $t[$k].FileDescription = 'FrameLedger.Agent'
        }
    }
    Invoke-Case 'a managed binary whose copyright is one space' 'empty: LegalCopyright' { param($t) $t['managed:FrameLedger.dll'].LegalCopyright = ' ' }
    Invoke-Case 'a managed copyright that is not the native one' 'different copyright lines' {
        param($t)
        $t['managed:FrameLedger.exe'].LegalCopyright = 'Copyright (C) 2026 poli0981'
        $t['managed:FrameLedger.dll'].LegalCopyright = 'Copyright (C) 2026 poli0981'
    }
    Invoke-Case 'a native company that is not FrameLedger' "CompanyName is 'poli0981'" { param($t) $t['native:FrameLedger.Overlay.dll'].CompanyName = 'poli0981' }
    Invoke-Case 'an apphost carrying another assembly''s block' "OriginalFilename is 'FrameLedger.dll', expected 'FrameLedger.Agent.dll'" {
        param($t) $t['managed:FrameLedger.Agent.exe'].OriginalFilename = 'FrameLedger.dll'
    }
    Invoke-Case 'an apphost its block was never copied into' 'no usable version block' {
        param($t)
        $b = $t['managed:FrameLedger.exe']
        foreach ($p in @($b.PSObject.Properties.Name)) { $b.$p = $null }
    }
    Invoke-Case 'a published assembly without the release''s version' "ProductVersion is '0.1.0', expected '0.1.0-beta.14'" { param($t) $t['managed:FrameLedger.dll'].ProductVersion = '0.1.0' }
    Invoke-Case 'a FileVersion that is not the VERSION file''s' "FileVersion is '0.0.0.0'" { param($t) $t['native:FrameLedger.Overlay.dll'].FileVersion = '0.0.0.0' }
    Invoke-Case 'a managed half with no version to compare with' 'no -ExpectedProductVersion' { } ''

    if ($failures.Count -gt 0) {
        Write-Host 'VERSIONINFO SELF-TEST FAILED' -ForegroundColor Red
        $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "versioninfo self-test: $($script:cases) cases, $($script:red) red, each for its own reason" -ForegroundColor Green
    exit 0
}

$errors = [System.Collections.Generic.List[string]]::new()
$blocks = [ordered]@{}
$nativeRead = 0
$managedRead = 0

if (-not $ManagedOnly) {
    if (-not (Test-Path $BuildDir)) {
        Write-Host "VERSIONINFO CHECK FAILED: no native build at $BuildDir" -ForegroundColor Red
        Write-Host '  Build first: ./build.ps1 native' -ForegroundColor Red
        exit 1
    }

    foreach ($name in $nativeBinaries) {
        $file = Get-ChildItem -Path $BuildDir -Filter $name -Recurse -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $file) {
            $errors.Add("$name was not produced by the native build — cannot verify it is identifiable")
            continue
        }

        $vi = $file.VersionInfo
        @(Test-Block $name 'native' $vi $ExpectedVersion '') | ForEach-Object { $errors.Add($_) }
        $blocks[$name] = $vi
        ++$nativeRead
        Write-Host ("  {0,-32} {1} / {2} / v{3}" -f $name, $vi.CompanyName, $vi.ProductName, $vi.FileVersion) -ForegroundColor DarkGray
    }
}

# `pwsh -File` hands `-ManagedDir a,b` over as ONE string; split it as the in-process call would have.
$ManagedDir = @($ManagedDir | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
foreach ($dir in $ManagedDir | Where-Object { -not (Test-Path $_) }) {
    $errors.Add("no managed build at $dir — build first: ./build.ps1 managed")
}

if ($ManagedDir.Count -gt 0) {
    foreach ($name in $managedBinaries) {
        $file = @($ManagedDir | Where-Object { Test-Path $_ } |
                ForEach-Object { Get-ChildItem -Path $_ -Filter $name -Recurse -File -ErrorAction SilentlyContinue }) |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if (-not $file) {
            $errors.Add("$name is not under $($ManagedDir -join ', ') — cannot verify it is identifiable")
            continue
        }

        $vi = $file.VersionInfo
        @(Test-Block $name 'managed' $vi $ExpectedVersion $ExpectedProductVersion) | ForEach-Object { $errors.Add($_) }
        $blocks[$name] = $vi
        ++$managedRead
        Write-Host ("  {0,-32} {1} / {2} / v{3}" -f $name, $vi.FileDescription, $vi.ProductName, $vi.ProductVersion) -ForegroundColor DarkGray
    }
}
elseif ($ManagedOnly) {
    $errors.Add('-ManagedOnly without -ManagedDir reads nothing, and a check of nothing is not a pass')
}

@(Test-OneCopyright $blocks) | ForEach-Object { $errors.Add($_) }

if ($errors.Count -gt 0) {
    Write-Host 'VERSIONINFO CHECK FAILED' -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host '  docs/19_SAFETY_AND_ANTICHEAT.md requires every shipped binary to identify itself (12_BUILD §Version).' -ForegroundColor Red
    exit 1
}

Write-Host "versioninfo OK — $nativeRead native and $managedRead managed binary(ies) identify themselves, one copyright" -ForegroundColor Green
exit 0
