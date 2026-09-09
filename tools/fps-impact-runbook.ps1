#requires -Version 7.0
<#
.SYNOPSIS
    Drives the ≤ 0.5 % FPS-impact measurement `14_TESTING` §Hook overhead item 2 requires, and
    writes the result block for `docs/spike-notes.md` §14.

.DESCRIPTION
    DEV-ONLY, and deliberately not part of `build.ps1 check`: it launches a real game, several times,
    and only the owner can read the game's own benchmark number off the screen. `build.ps1` must stay
    a script CI can run unattended.

    WHAT IT DOES NOT DO, stated first because a runbook that looked like a measurement tool would be
    the worst version of this file. It does not measure FPS. It cannot: FrameLedger's own frame times
    exist only in the hooked runs, so using them for both legs would compare a measurement against
    nothing, and using them for one leg would compare two different instruments. The number that
    decides the gate is **the game's own benchmark result**, typed in by the operator after each run.
    This script sequences the runs, holds the consent state right for each leg, records what it CAN
    read without a game (the Agent's CPU and RSS, the session row of every hooked run), and does the
    arithmetic and the formatting so a six-run comparison is not re-derived by hand at midnight.

    THE TWO LEGS ARE NOT SYMMETRIC, and that is the point of the sequencing.
      ON  — hooking enabled and consented for the title; the Agent LAUNCHES it (`--console launch`),
             which is the mode a user gets and the only mode Vulkan can be captured in.
      OFF — the SAME executable, launched the same way, with hooking DISABLED for it
             (`--console consent revoke`): the Agent still records a Tier-2 session, so the process
             tree, the working set and the operator's actions are as alike as they can be made. The
             only difference is the injection. Running the game outside FrameLedger entirely would
             also drop the watcher, the ledger write and the telemetry poller from the comparison,
             and then a delta could not be attributed to the hook.

    ONE GAME LAUNCH PER CAPTURE (HANDOFF §Traps). A capture that ends kills the Overlay in that
    process 65 s later, permanently, so each run is its own launch of the game — six launches, not
    two with three captures each.

.PARAMETER Exe
    The game's executable. It is CONSENTED and REVOKED by this script, so it must be a title the
    operator has decided to measure; nothing here bypasses the disclosure — the first ON run prompts
    for it unless a record already exists.

.PARAMETER Runs
    Runs per leg. `14_TESTING` item 2 says 3.

.PARAMETER Seconds
    Bound on each capture, matched to the built-in benchmark's length. 0 = until the title exits.

.PARAMETER GameArgs
    The game's own command line (a benchmark switch, a map, a `-nointro`).

.PARAMETER DataDir
    Optional scratch ledger. Omit to use the Agent's own `%LOCALAPPDATA%\FrameLedger`, which is what
    a real measurement should use — the runbook's own sessions belong in the operator's history.

.EXAMPLE
    ./tools/fps-impact-runbook.ps1 -Exe 'D:\Games\Title\game.exe' -GameArgs '-benchmark' -Seconds 600
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Exe,

    [int]$Runs = 3,

    [int]$Seconds = 0,

    # NOT named -Args: $Args is PowerShell's automatic argument array, and a parameter that shadows it
    # works only while this stays an advanced function. The footgun is not worth the four characters.
    [string]$GameArgs = '',

    [string]$DataDir,

    [string]$Agent = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/FrameLedger.Agent/bin/Release/net10.0-windows10.0.22621.0/win-x64/FrameLedger.Agent.exe')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Agent)) {
    throw "FrameLedger.Agent.exe not found at $Agent. Build it first (./build.ps1 check) or pass -Agent."
}
if (-not (Test-Path -LiteralPath $Exe)) {
    throw "No such executable: $Exe"
}

$consoleArgs = @('--console')
if ($DataDir) { $consoleArgs += @('--data-dir', $DataDir) }

function Invoke-Agent {
    param([string[]]$Verb, [switch]$Interactive)
    $all = $consoleArgs + $Verb
    Write-Host "  > FrameLedger.Agent $($all -join ' ')" -ForegroundColor DarkGray
    if ($Interactive) {
        # `consent grant` refuses redirected stdin by design: the operator types the phrase.
        & $Agent @all
        return @{ Output = ''; Exit = $LASTEXITCODE }
    }
    $out = & $Agent @all 2>&1 | Out-String
    return @{ Output = $out; Exit = $LASTEXITCODE }
}

function Read-Number {
    param([string]$Prompt)
    while ($true) {
        $raw = Read-Host $Prompt
        $value = 0.0
        if ([double]::TryParse($raw, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$value) -and $value -gt 0) {
            return $value
        }
        Write-Host '  a positive number, as the game reported it (no units)' -ForegroundColor Yellow
    }
}

# One run of one leg: launch, wait for the Agent to finish, read what we can, ask what we cannot.
function Invoke-Leg {
    param([string]$Leg, [int]$Index)

    Write-Host ''
    Write-Host "[$Leg $Index/$Runs] launch the title, run the SAME built-in benchmark, then read its result." -ForegroundColor Cyan
    Read-Host '  press Enter to launch'

    $verb = @('launch', '--exe', $Exe)
    if ($GameArgs) { $verb += @('--args', $GameArgs) }
    if ($Seconds -gt 0) { $verb += @('--seconds', "$Seconds") }

    $started = Get-Date
    $agentProc = Start-Process -FilePath $Agent -ArgumentList ($consoleArgs + $verb) -PassThru -NoNewWindow -RedirectStandardOutput ([IO.Path]::GetTempFileName())
    $cpuPeak = 0.0
    $rssPeak = 0L
    while (-not $agentProc.HasExited) {
        try {
            $agentProc.Refresh()
            $rssPeak = [Math]::Max($rssPeak, $agentProc.WorkingSet64)
            $cpuPeak = [Math]::Max($cpuPeak, $agentProc.TotalProcessorTime.TotalSeconds)
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    $wall = ((Get-Date) - $started).TotalSeconds
    $output = Get-Content -LiteralPath $agentProc.StartInfo.RedirectStandardOutputFile -Raw -ErrorAction SilentlyContinue

    # CPU as a share of ONE core over the run's wall clock — the unit 14_TESTING item 2 states.
    $cpuPct = if ($wall -gt 0) { [Math]::Round(100.0 * $cpuPeak / $wall, 2) } else { 0 }
    $rssMb = [Math]::Round($rssPeak / 1MB, 1)

    Write-Host "  agent: CPU $cpuPct % of a core, RSS $rssMb MB, wall $([Math]::Round($wall,1)) s" -ForegroundColor DarkGray
    $fps = Read-Number "  the GAME's own average FPS for this run"

    return [pscustomobject]@{
        Leg    = $Leg
        Index  = $Index
        Fps    = $fps
        CpuPct = $cpuPct
        RssMb  = $rssMb
        Wall   = [Math]::Round($wall, 1)
        Row    = ($output -split "`n" | Where-Object { $_ -match '^\s+row: ' } | Select-Object -First 1)
    }
}

Write-Host 'FPS-impact runbook — 14_TESTING §Hook overhead item 2' -ForegroundColor Cyan
Write-Host "  title   : $Exe"
Write-Host "  runs    : $Runs per leg (ON = hooked, OFF = same launch path, hooking revoked)"
Write-Host "  ledger  : $(if ($DataDir) { $DataDir } else { 'the Agent profile' })"
Write-Host ''
Write-Host 'Use the SAME built-in benchmark, the same settings and the same scene for all six runs.' -ForegroundColor Yellow
Write-Host 'Close everything else. Do not alt-tab during a run.' -ForegroundColor Yellow

# --- ON leg: consent first (the disclosure is the operator's, and this cannot bypass it) -----------
Write-Host ''
Write-Host 'ON leg — granting consent for this title (type the phrase when asked).' -ForegroundColor Cyan
$grant = Invoke-Agent -Verb @('consent', 'grant', '--exe', $Exe) -Interactive
if ($grant.Exit -ne 0) {
    throw 'consent was not granted, so there is no ON leg to measure. Nothing was changed.'
}

$results = @()
foreach ($i in 1..$Runs) { $results += Invoke-Leg -Leg 'ON' -Index $i }

# --- OFF leg: same launch path, hooking revoked ----------------------------------------------------
Write-Host ''
Write-Host 'OFF leg — revoking consent; the Agent still records each run as Tier 2.' -ForegroundColor Cyan
$revoke = Invoke-Agent -Verb @('consent', 'revoke', '--exe', $Exe)
if ($revoke.Exit -ne 0) {
    throw "revoke failed, so the OFF leg would still be hooked:`n$($revoke.Output)"
}

foreach ($i in 1..$Runs) { $results += Invoke-Leg -Leg 'OFF' -Index $i }

# --- the arithmetic --------------------------------------------------------------------------------
$on = @($results | Where-Object { $_.Leg -eq 'ON' })
$off = @($results | Where-Object { $_.Leg -eq 'OFF' })
$onMean = ($on.Fps | Measure-Object -Average).Average
$offMean = ($off.Fps | Measure-Object -Average).Average
$deltaPct = if ($offMean -gt 0) { 100.0 * ($offMean - $onMean) / $offMean } else { [double]::NaN }
$cpuMax = ($on.CpuPct | Measure-Object -Maximum).Maximum
$rssMax = ($on.RssMb | Measure-Object -Maximum).Maximum

$verdictFps = if ($deltaPct -le 0.5) { 'PASS' } else { 'FAIL' }
$verdictCpu = if ($cpuMax -le 1.0) { 'PASS' } else { 'FAIL' }
$verdictRss = if ($rssMax -le 150.0) { 'PASS' } else { 'FAIL' }

$lines = @()
$lines += "### FPS impact — $(Split-Path -Leaf $Exe), $(Get-Date -Format 'yyyy-MM-dd')"
$lines += ''
$lines += "Benchmark: **fill in** (the built-in run used, settings, resolution). Driver: **fill in**."
$lines += ''
$lines += '| Run | Leg | Game avg FPS | Agent CPU (% of a core) | Agent RSS (MB) | Wall (s) |'
$lines += '|---|---|---|---|---|---|'
foreach ($r in $results) {
    $lines += "| $($r.Index) | $($r.Leg) | $($r.Fps) | $($r.CpuPct) | $($r.RssMb) | $($r.Wall) |"
}
$lines += ''
$lines += "**Mean ON $([Math]::Round($onMean,2)) FPS · mean OFF $([Math]::Round($offMean,2)) FPS · delta $([Math]::Round($deltaPct,3)) % — $verdictFps** (gate: ≤ 0.5 %)."
$lines += "Agent CPU peak $cpuMax % of a core — $verdictCpu (gate: ≤ 1 %). Agent RSS peak $rssMax MB — $verdictRss (gate: ≤ 150 MB)."
$lines += ''
$lines += 'Overlay resident set is NOT measured here (`14_TESTING` item 2 also names ≤ 8 MB): it is a module inside the game process, and this script does not read the target''s memory — that is CLAUDE.md rule 4. Read it with a tool that inspects the process from outside, or leave the row unmeasured rather than guessed.'
$lines += ''
foreach ($r in $on) {
    if ($r.Row) { $lines += "> ON $($r.Index): $($r.Row.Trim())" }
}

Write-Host ''
Write-Host ($lines -join [Environment]::NewLine)
Write-Host ''
Write-Host 'Paste the block above into docs/spike-notes.md §14, fill in the benchmark and driver, and update' -ForegroundColor Cyan
Write-Host '14_TESTING §Hook overhead item 2 with the delta. The consent record is left REVOKED — grant it again' -ForegroundColor Cyan
Write-Host 'if you want the title hooked from here on.' -ForegroundColor Cyan
