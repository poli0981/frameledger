#Requires -Version 7.0
<#
.SYNOPSIS
    Fails when a test run left hook-harness overlay logs in the real FrameLedger data folder.

.DESCRIPTION
    docs/17_HOOK_ENGINE.md §Native logging. The injection tests use the shipped Overlay, and the Overlay writes its log
    to %LOCALAPPDATA%\FrameLedger\logs\overlay-<pid>-<stamp>.log: the real per-user data folder, by design (§S21, not a
    parameter). Measured 2026-09-15, that folder held 2506 overlay logs, 2492 of them hook-harness's, left by test runs.

    So every test binary that starts hook-harness removes, when its run ends, the logs it caused: the Catch2 listener in
    src/native/tests/guard_test.cpp and tests/Shared/HarnessOverlayLogSweep.cs, which FrameLedger.DrainFixtures.targets
    compiles into each managed test project that stages the harness. This gate is what makes a missing sweep red: an
    overlay log created since the gate started whose first line names a hook-harness image, still there after the tests,
    fails it. A game's log names the game and never counts; a log from before the gate never counts.

.PARAMETER SinceUtc
    The gate's start. Only logs created at or after it count.

.PARAMETER LogsDirectory
    The directory to read. Default: the Overlay's own, %LOCALAPPDATA%\FrameLedger\logs.

.PARAMETER SelfTest
    Runs the fixture cases below, in scratch directories, and exits 0 only if every one behaves.
#>
[CmdletBinding()]
param(
    [DateTime]$SinceUtc = [DateTime]::UtcNow,
    [string]$LogsDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FrameLedger\logs'),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LeftHarnessLog {
    param([string]$Directory, [DateTime]$Since)

    $left = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { return , $left.ToArray() }
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Filter 'overlay-*.log' -File) {
        if ($file.CreationTimeUtc -lt $Since) { continue }
        $first = $null
        try {
            $stream = [IO.FileStream]::new($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
            try {
                # The image comes from GetModuleFileNameA, in the ANSI code page; Latin-1 reads any byte.
                $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::Latin1)
                $first = $reader.ReadLine()
            }
            finally { $stream.Dispose() }
        }
        catch [IO.IOException] { continue }
        if ($null -eq $first) { continue }
        $m = [regex]::Match($first, '^# FrameLedger\.Overlay build .*? image (?<image>.+?)\s*$', 'CultureInvariant')
        if (-not $m.Success) { continue }
        $image = $m.Groups['image'].Value
        $name = [IO.Path]::GetFileName($image)
        if ($name.StartsWith('hook-harness', [StringComparison]::OrdinalIgnoreCase)) {
            $left.Add("$($file.Name)  (image $image)")
        }
    }
    return , $left.ToArray()
}

if ($SelfTest) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('fl-artifacts-selftest-' + [guid]::NewGuid().ToString('N'))
    $since = [DateTime]::UtcNow.AddSeconds(-1)
    $header = '# FrameLedger.Overlay build test pid 101 layout v9 image '
    $cases = @(
        @{ Name = "a harness log created during the run FAILS"; File = 'overlay-1-20260915-010101.log'; Text = "${header}C:\build\tools\hook-harness\hook-harness.exe`nRING_CREATED`n"; Left = 1 },
        @{ Name = "a renamed harness copy's log FAILS"; File = 'overlay-2-20260915-010101.log'; Text = "${header}C:\Temp\fl-pipe-e2e-1\HOOK-HARNESS-fl-pipe-e2e-1.exe`r`n"; Left = 1 },
        @{ Name = "a game's log passes"; File = 'overlay-3-20260915-010101.log'; Text = "${header}D:\Games\Title\title.exe`n"; Left = 0 },
        @{ Name = 'a harness log from before the run passes'; File = 'overlay-4-19990101-000000.log'; Text = "${header}C:\build\hook-harness.exe`n"; Old = $true; Left = 0 },
        @{ Name = 'an overlay-named file with no Overlay header passes'; File = 'overlay-5-20260915-010101.log'; Text = "# planted by guard_test`n"; Left = 0 },
        @{ Name = 'a non-overlay log with a harness header passes'; File = 'agent-20260915.log'; Text = "${header}C:\build\hook-harness.exe`n"; Left = 0 },
        @{ Name = 'a directory that does not exist passes'; File = $null; Left = 0 }
    )
    $failed = 0
    try {
        $i = 0
        foreach ($c in $cases) {
            $i++
            $dir = Join-Path $root "case$i"
            if ($c.File) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                $path = Join-Path $dir $c.File
                [IO.File]::WriteAllText($path, $c.Text)
                if ($c.ContainsKey('Old')) { [IO.File]::SetCreationTimeUtc($path, [DateTime]::new(1999, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)) }
            }
            $got = Get-LeftHarnessLog -Directory $dir -Since $since
            $ok = $got.Count -eq $c.Left
            Write-Host ("  {0,-4} {1}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
            if (-not $ok) { $failed++ }
        }
    }
    finally {
        if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    }
    if ($failed -gt 0) { Write-Host "test-artifacts self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host "test-artifacts self-test: $($cases.Count) cases, both directions" -ForegroundColor Green
    exit 0
}

$left = Get-LeftHarnessLog -Directory $LogsDirectory -Since $SinceUtc
if ($left.Count -gt 0) {
    $left | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    if ($left.Count -gt 20) { Write-Host "  ... and $($left.Count - 20) more" -ForegroundColor Red }
    Write-Host ("test-artifacts: {0} hook-harness overlay log(s) created during this run are still in the data folder. A test binary that starts hook-harness is missing its sweep (17_HOOK_ENGINE §Native logging)." -f $left.Count) -ForegroundColor Red
    exit 1
}
Write-Host 'test-artifacts: ok - no hook-harness overlay log from this run is left in the data folder' -ForegroundColor Green
exit 0
