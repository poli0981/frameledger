# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Fails when a shipped native binary does not carry the release's build id, exactly.

.DESCRIPTION
    FL_BUILD_ID is `git describe` at CMake's configure (src/native/FrameLedger.Shm/CMakeLists.txt), compiled into the
    Overlay, the guard, the NVAPI bridge and the process-stats reader; the ring's handshake compares the Overlay's with the
    guard's, and the App shows the Overlay's as "Overlay build". release.yml wrote the release date into the tracked
    legal/*.md before the gate configured, so describe read the tree as dirty and every release until beta.18 shipped
    "<tag>-dirty" (the owner's Dashboard, 0.1.0-beta.16 and beta.17). The workflow now reads the id from the clean
    checkout, hands it to CMake as the environment's FL_BUILD_ID, and runs this over the published tree.

    The id is looked for as the C string it is compiled as — its characters and the NUL after them — in each binary's
    bytes. Nothing is loaded: loading the Overlay would run its DllMain in this process.

.PARAMETER BuildDir
    The directory holding the native binaries (release.yml: out/app).

.PARAMETER Expected
    The id they must carry: describe of the clean checkout. One that marks a dirty tree is refused too.

.PARAMETER SelfTest
    Runs the fixture cases below, in a scratch directory, and exits 0 only if every one behaves.
#>
[CmdletBinding()]
param(
    [string]$BuildDir,
    [string]$Expected,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The four that compile FL_BUILD_ID in (FrameLedger.Shm's INTERFACE define): FlGetBuildId, FlGuardBuildId, FlNvBuildId, FlPsBuildId.
$binaries = 'FrameLedger.Overlay.dll', 'FrameLedger.Guard.dll', 'FrameLedger.NvapiBridge.dll', 'FrameLedger.ProcessStats.dll'

function Get-BuildIdProblem {
    param([string]$Directory, [string]$Id)

    $problems = [System.Collections.Generic.List[string]]::new()
    # What describe can print, and what a C string literal carries without escaping (CMakeLists.txt checks the same).
    if ([string]::IsNullOrEmpty($Id) -or $Id -notmatch '^[0-9A-Za-z._+-]{1,31}$') {
        $problems.Add("the expected id '$Id' is not a build id (1 to 31 of [0-9A-Za-z._+-])")
        return , $problems.ToArray()
    }
    if ($Id.EndsWith('-dirty', [StringComparison]::Ordinal) -or $Id.EndsWith('+', [StringComparison]::Ordinal)) {
        $problems.Add("the expected id '$Id' marks a dirty tree; a release is built from a clean checkout")
    }
    # Latin-1 maps every byte to one char, so a byte search is an ordinal string search.
    $needle = $Id + [char]0
    foreach ($name in $binaries) {
        $path = Join-Path $Directory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $problems.Add("$name is missing")
            continue
        }
        $text = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($path))
        if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            $problems.Add("$name does not carry '$Id'")
        }
    }
    return , $problems.ToArray()
}

if ($SelfTest) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('fl-buildid-selftest-' + [guid]::NewGuid().ToString('N'))
    $id = 'v0.1.0-beta.18'
    $cases = @(
        @{ Name = 'every binary carries the id passes'; Expected = $id; Ids = @($id, $id, $id, $id); Bad = 0 },
        @{ Name = 'a commit id with no tag passes'; Expected = '15b4c6b1a2c3'; Ids = @('15b4c6b1a2c3', '15b4c6b1a2c3', '15b4c6b1a2c3', '15b4c6b1a2c3'); Bad = 0 },
        @{ Name = "the Overlay built from a dirty tree FAILS"; Expected = $id; Ids = @("$id-dirty", $id, $id, $id); Bad = 1 },
        @{ Name = "the short fallback's dirty mark FAILS"; Expected = $id; Ids = @($id, "$id+", $id, $id); Bad = 1 },
        @{ Name = 'a binary with no id at all FAILS'; Expected = $id; Ids = @($id, $id, 'unknown', $id); Bad = 1 },
        @{ Name = 'a missing binary FAILS'; Expected = $id; Ids = @($id, $id, $id, $null); Bad = 1 },
        @{ Name = 'an expected id that marks a dirty tree FAILS'; Expected = "$id-dirty"; Ids = @("$id-dirty", "$id-dirty", "$id-dirty", "$id-dirty"); Bad = 1 },
        @{ Name = 'an expected id no build has FAILS'; Expected = 'v0.1.0 "beta"'; Ids = @($id, $id, $id, $id); Bad = 1 }
    )
    $failed = 0
    try {
        $i = 0
        foreach ($c in $cases) {
            $i++
            $dir = Join-Path $root "case$i"
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            for ($b = 0; $b -lt $binaries.Count; $b++) {
                if ($null -eq $c.Ids[$b]) { continue }
                # A string among other bytes, as the linker leaves it in .rdata.
                $body = [Text.Encoding]::Latin1.GetBytes("MZ`0`0padding`0" + $c.Ids[$b] + "`0more`0")
                [IO.File]::WriteAllBytes((Join-Path $dir $binaries[$b]), $body)
            }
            $got = Get-BuildIdProblem -Directory $dir -Id $c.Expected
            $ok = ($got.Count -gt 0) -eq ($c.Bad -gt 0)
            Write-Host ("  {0,-4} {1}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
            if (-not $ok) { $failed++ }
        }
    }
    finally {
        if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    }
    if ($failed -gt 0) { Write-Host "buildid-check self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host "buildid-check self-test: $($cases.Count) cases, both directions" -ForegroundColor Green
    exit 0
}

if (-not $BuildDir) { throw 'buildid-check: -BuildDir is required (or -SelfTest)' }
$problems = Get-BuildIdProblem -Directory $BuildDir -Id $Expected
if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host "buildid-check: the native binaries in $BuildDir do not all carry '$Expected' (src/native/FrameLedger.Shm/CMakeLists.txt, 12_BUILD)." -ForegroundColor Red
    exit 1
}
Write-Host "buildid-check: ok - $($binaries.Count) native binaries carry '$Expected'" -ForegroundColor Green
exit 0
