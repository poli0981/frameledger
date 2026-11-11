# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    What the shipped Agent costs while it records a session: processor time per second of session, measured on the
    real FrameLedger.Agent.exe, with Advanced metrics off and on.

.DESCRIPTION
    docs/20_OPEN_QUESTIONS.md §M16 (owner decision D110, 2026-11-11): a number read off a probe is not the number of
    a session. This runs `FrameLedger.Agent.exe --console capture` against a copy of hook-harness - the only thing
    this project's tools ever point the Agent at - in a scratch data folder, never the profile's, and reads the
    Agent process's own processor time when it exits.

    A capture of an executable nobody consented to is a session that is not hooked: the Agent injects nothing, and
    the telemetry poller runs exactly as in every session (L1 + LibreHardwareMonitor + NVAPI, the processor and
    memory counters, the game's own memory). That isolates what §M16 asks about - the cost of telemetry - from the
    cost of draining frames.

    One run includes the Agent's start (the host, the ledger's migration, the sensor library opening), so a single
    run says little. Each mode is run at two lengths and the cost per second is the slope between them:
    (processor time of the long run - of the short run) / (long - short).

    Developer tool, not a gate: it needs a release build (./build.ps1 native, then a Release build of the solution),
    Python 3 on the path to write one row of the scratch ledger's `settings` table, and a few minutes.

.PARAMETER Seconds
    The two session lengths, short then long.

.PARAMETER Repeats
    How many times each (mode, length) is run; the median is reported.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [int[]]$Seconds = @(30, 90),
    [int]$Repeats = 2,
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Seconds.Count -ne 2 -or $Seconds[0] -ge $Seconds[1]) { throw 'Seconds takes two lengths, short then long' }

$tfm = 'net11.0-windows10.0.22621.0'
$agent = Join-Path $RepoRoot "src/FrameLedger.Agent/bin/$Configuration/$tfm/win-x64/FrameLedger.Agent.exe"
$harness = Join-Path $RepoRoot "tests/FrameLedger.Agent.Tests/bin/$Configuration/$tfm/win-x64/hook-harness.exe"
foreach ($f in $agent, $harness) { if (-not (Test-Path $f)) { throw "not built: $f (./build.ps1 native, then build the solution in $Configuration)" } }

$run = [guid]::NewGuid().ToString('N').Substring(0, 12)
$scratch = Join-Path ([IO.Path]::GetTempPath()) "fl-agent-cost-$run"
$data = Join-Path $scratch 'data'
$profileData = Join-Path $env:LOCALAPPDATA 'FrameLedger'
if ($data.StartsWith($profileData, [StringComparison]::OrdinalIgnoreCase)) { throw 'the scratch data folder may never be the profile''s' }
New-Item -ItemType Directory -Force $data | Out-Null

# A copy under a name of this run's own: the Agent finds its target by executable, and another hook-harness.exe
# running on this machine - a test suite's - would be a second match.
$target = Join-Path $scratch "hook-harness-$run.exe"
Copy-Item $harness $target

function Set-Advanced([bool]$On) {
    $db = Join-Path $data 'ledger.db'
    $value = if ($On) { '1' } else { '0' }
    $code = "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute('INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value', ('telemetry.advanced', sys.argv[2])); c.commit(); c.close()"
    & python -c $code $db $value
    if ($LASTEXITCODE -ne 0) { throw 'could not write telemetry.advanced into the scratch ledger (is Python 3 on the path?)' }
}

function Measure-Capture([int]$Length) {
    $game = Start-Process -FilePath $target -ArgumentList '--real', '--hold-presenting', ($Length + 40) -PassThru -WindowStyle Hidden
    try {
        Start-Sleep -Seconds 4
        $psi = [Diagnostics.ProcessStartInfo]::new($agent)
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        foreach ($a in '--console', '--data-dir', $data, 'capture', '--exe', $target, '--seconds', "$Length") { $psi.ArgumentList.Add($a) }
        $p = [Diagnostics.Process]::Start($psi)
        $null = $p.Handle
        $out = $p.StandardOutput.ReadToEndAsync()
        $err = $p.StandardError.ReadToEndAsync()
        $peak = 0L
        while (-not $p.WaitForExit(1000)) {
            try { $p.Refresh(); $peak = [Math]::Max($peak, $p.WorkingSet64) } catch { }
        }
        $p.WaitForExit()
        $text = $out.Result
        # The verb's exit code says the hook was refused (2) - which is the point here; what matters is that the session was stored.
        if ($text -notmatch 'SAVED as sessions\.id=\d+ \(tier 2') { throw "the capture did not end as a stored, unhooked session (exit $($p.ExitCode)):`n$text`n$($err.Result)" }
        $layers = if ($text -match 'telemetry=(\S+)') { $Matches[1] } else { '?' }
        [pscustomobject]@{ Seconds = $Length; CpuMs = $p.TotalProcessorTime.TotalMilliseconds; PeakMiB = [Math]::Round($peak / 1MB, 1); Layers = $layers }
    }
    finally {
        if (-not $game.HasExited) { $game.Kill($true) }
    }
}

function Median([double[]]$Values) {
    $s = @($Values | Sort-Object)
    if ($s.Count % 2) { $s[($s.Count - 1) / 2] } else { ($s[$s.Count / 2 - 1] + $s[$s.Count / 2]) / 2 }
}

try {
    # The first run creates and migrates the scratch ledger; it is not measured.
    $null = Measure-Capture 5
    $rows = @()
    foreach ($mode in $false, $true) {
        Set-Advanced $mode
        foreach ($length in $Seconds) {
            foreach ($i in 1..$Repeats) {
                $m = Measure-Capture $length
                $rows += [pscustomobject]@{ Advanced = $mode; Seconds = $length; CpuMs = [Math]::Round($m.CpuMs); PeakMiB = $m.PeakMiB; Layers = $m.Layers }
                Write-Host ("  advanced={0,-5} {1,3} s  cpu {2,6:N0} ms  peak {3,6:N1} MiB  {4}" -f $mode, $length, $m.CpuMs, $m.PeakMiB, $m.Layers)
            }
        }
    }

    Write-Host ''
    foreach ($mode in $false, $true) {
        $short = Median @($rows | Where-Object { $_.Advanced -eq $mode -and $_.Seconds -eq $Seconds[0] } | ForEach-Object CpuMs)
        $long = Median @($rows | Where-Object { $_.Advanced -eq $mode -and $_.Seconds -eq $Seconds[1] } | ForEach-Object CpuMs)
        $perSecond = ($long - $short) / ($Seconds[1] - $Seconds[0])
        $peak = ($rows | Where-Object { $_.Advanced -eq $mode } | Measure-Object PeakMiB -Maximum).Maximum
        Write-Host ("advanced={0,-5}: {1:N1} ms of processor time per second of session = {2:N2} % of one core (median {3:N0} ms over {4} s, {5:N0} ms over {6} s); peak working set {7:N1} MiB" -f `
                $mode, $perSecond, ($perSecond / 10), $short, $Seconds[0], $long, $Seconds[1], $peak)
    }
}
finally {
    Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
}
