# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Every NVAPI entry point the bridge calls is on a committed list, and nothing on that list writes or identifies.

.DESCRIPTION
    docs/18_GPU_VENDOR_APIS.md section Runtime policy has said since the first telemetry layer: "Read-only, always.
    FrameLedger never calls a setter." For two months that was a sentence a reviewer was asked to check by reading
    one file. "Advanced metrics" (owner decisions D101 and D108, 2026-11-11) took the bridge from fourteen NVAPI calls
    to forty-four, and NVAPI's header declares a setter beside almost every getter - NvAPI_DRS_SetSetting beside
    NvAPI_DRS_GetSetting, NvAPI_Disp_SetSourceColorSpace two lines above NvAPI_Disp_GetSourceColorSpace. A sentence
    does not scale to that; this does.

    WHAT IT HOLDS, on src/native/FrameLedger.NvapiBridge/src/fl_nvapi_bridge.cpp - the one translation unit that
    links nvapi64.lib into a shipped binary - against tools/nvapi-allowed-calls.txt:

      1. every NvAPI_* call in the source is on the list;
      2. every name on the list is called (a list longer than the code is a list nobody reads);
      3. no name on the list sets, saves, restores, deletes, registers, resets, enables, disables or controls
         anything - whatever the list says. The two exceptions are spelled out below, by name;
      4. no name on the list returns an identifier (D104): a serial number, a unique id, an EDID, the list of the
         programs that are encoding.

    Rules 3 and 4 judge the LIST, not the code, so the way to let a writer in is not "add a line to the text file".

    WHAT IT DOES NOT DO. It reads names, not arguments: an entry point that reads or writes depending on a command
    argument (NvAPI_Disp_ColorControl, NvAPI_Disp_HdrColorControl) could not be told apart by it, which is why rule 3
    refuses every name ending in Control outright. It does not look at the other native targets: no other shipped
    target links nvapi64.lib (the Overlay links none of it - tools/vendor-exports and the package closure hold that),
    and the probe tools are not shipped.

    -SelfTest proves every rule in both directions on in-memory sources.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# A verb that changes something, as a whole camel-case word: a capital first, then a capital, an underscore or the
# end after it. So "SetSetting", "ClientRegisterFor..." and "ColorControl" match, and "GetThermalSettings",
# "LoadSettings", "GetSetting" and "GetAllClockFrequencies" do not. It errs towards red: a noun that happens to end
# in one of these words is refused until it is named below.
$script:WriterVerb = '(Set|Save|Restore|Delete|Remove|Register|Unregister|Reset|Enable|Disable|Flash|Write|Apply|Lock|Unlock|Create|Destroy|Control)(?=[A-Z_]|$)'

# The names the verb rule would refuse and that are reads. Named here one by one, each with its reason - never by a
# pattern, so an exemption cannot grow to cover a writer.
$script:WriterExempt = @(
    'NvAPI_DRS_CreateSession',     # the handle of a settings session that is only ever read from
    'NvAPI_DRS_DestroySession',    # ... and its release
    'NvAPI_SYS_GetChipSetInfo'     # "ChipSet" is a noun: the chipset's vendor and name
)

# D104. NVAPI's entry points that return something identifying a device, a person or another program.
$script:Identifier = '(EDID|Edid|UUID|Serial|BoardInfo|EncoderSessionsInfo)'

# The NvAPI_* names a C++ source CALLS: comments and string literals are taken out first, so a setter named in the
# comment that says it is never called is not a call.
function Get-NvapiCall([string]$Source) {
    $code = [regex]::Replace($Source, '/\*.*?\*/', ' ', 'Singleline')
    $code = [regex]::Replace($code, '//[^\r\n]*', ' ')
    $code = [regex]::Replace($code, '"(\\.|[^"\\\r\n])*"', '""')
    $names = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($m in [regex]::Matches($code, '\bNvAPI_\w+(?=\s*\()')) { [void]$names.Add($m.Value) }
    return , @($names)
}

# The names of the list, and what is wrong with the list itself.
function Read-AllowedList([string]$Text) {
    $names = [System.Collections.Generic.List[string]]::new()
    $problems = [System.Collections.Generic.List[string]]::new()
    $number = 0
    foreach ($raw in ($Text -split "`r?`n")) {
        $number++
        $line = $raw.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
        if ($line -cnotmatch '^NvAPI_\w+$') { $problems.Add("the list's line $number is not one NvAPI name: '$line'"); continue }
        if ($names.Contains($line)) { $problems.Add("the list names $line twice"); continue }
        $names.Add($line)
    }
    return @{ Names = @($names); Problems = @($problems) }
}

function Test-Bridge([string]$Source, [string]$ListText) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $list = Read-AllowedList $ListText
    foreach ($p in $list.Problems) { $problems.Add($p) }
    $allowed = $list.Names
    $called = Get-NvapiCall $Source

    if ($called.Count -eq 0) { $problems.Add('no NvAPI_* call was found in the bridge source: the check read the wrong file, or nothing') }
    foreach ($name in $called) {
        if ($allowed -cnotcontains $name) { $problems.Add("the bridge calls $name, which the list does not allow") }
    }
    foreach ($name in $allowed) {
        if ($called -cnotcontains $name) { $problems.Add("the list allows $name, which the bridge does not call") }
        if ($name -cmatch $script:WriterVerb -and $script:WriterExempt -cnotcontains $name) {
            $problems.Add("$name changes something (its name carries '$($Matches[1])'): the bridge is read-only, whatever the list says")
        }
        if ($name -cmatch $script:Identifier) {
            $problems.Add("$name returns an identifier ('$($Matches[1])'): never read (D104), whatever the list says")
        }
    }
    return , @($problems)
}

if ($SelfTest) {
    $failed = 0
    $goodSource = @'
// NvAPI_DRS_SetSetting is never called.
int Read() {
    NvAPI_Initialize();
    /* nor NvAPI_GPU_SetEDID(
       anywhere */
    Log("NvAPI_DRS_SaveSettings(");
    return NvAPI_GPU_GetThermalSettings (g, 0, &t) + NvAPI_DRS_CreateSession(&s) + NvAPI_DRS_GetSetting(s, p, id, &v);
}
'@
    $goodList = "# a comment`nNvAPI_Initialize`n`nNvAPI_GPU_GetThermalSettings`nNvAPI_DRS_CreateSession`nNvAPI_DRS_GetSetting`n"
    $cases = @(
        @{ Name = 'a source whose calls are the list passes, with setters named only in comments and strings'; Source = $goodSource; List = $goodList; Expect = 0 },
        @{ Name = 'a call the list does not allow FAILS'; Source = $goodSource + "`nvoid X() { NvAPI_GPU_GetTachReading(g, &v); }"; List = $goodList; Expect = 1 },
        @{ Name = 'a name on the list that nothing calls FAILS'; Source = $goodSource; List = $goodList + "NvAPI_GPU_GetFullName`n"; Expect = 1 },
        @{ Name = 'a setter FAILS even when it is listed and called'; Source = $goodSource + "`nvoid X() { NvAPI_DRS_SetSetting(s, p, &v); }"; List = $goodList + "NvAPI_DRS_SetSetting`n"; Expect = 1 },
        @{ Name = 'a save FAILS even when it is listed and called'; Source = $goodSource + "`nvoid X() { NvAPI_DRS_SaveSettings(s); }"; List = $goodList + "NvAPI_DRS_SaveSettings`n"; Expect = 1 },
        @{ Name = 'a registered callback FAILS even when it is listed and called'; Source = $goodSource + "`nvoid X() { NvAPI_GPU_ClientRegisterForUtilizationSampleUpdates(g, &v); }"; List = $goodList + "NvAPI_GPU_ClientRegisterForUtilizationSampleUpdates`n"; Expect = 1 },
        @{ Name = 'an entry point that reads or writes by a command FAILS'; Source = $goodSource + "`nvoid X() { NvAPI_Disp_ColorControl(id, &v); }"; List = $goodList + "NvAPI_Disp_ColorControl`n"; Expect = 1 },
        @{ Name = 'a reader of an EDID FAILS even when it is listed and called'; Source = $goodSource + "`nvoid X() { NvAPI_DISP_GetEdidData(id, &e, &f); }"; List = $goodList + "NvAPI_DISP_GetEdidData`n"; Expect = 1 },
        @{ Name = 'a reader of the board serial FAILS'; Source = $goodSource + "`nvoid X() { NvAPI_GPU_GetBoardInfo(g, &b); }"; List = $goodList + "NvAPI_GPU_GetBoardInfo`n"; Expect = 1 },
        @{ Name = 'the list of encoding programs FAILS, and the statistics beside it pass'; Source = $goodSource + "`nvoid X() { NvAPI_GPU_GetEncoderSessionsInfo(g, &i); NvAPI_GPU_GetEncoderStatistics(g, &e); }"; List = $goodList + "NvAPI_GPU_GetEncoderSessionsInfo`nNvAPI_GPU_GetEncoderStatistics`n"; Expect = 1 },
        @{ Name = 'a session handle destroyed passes'; Source = $goodSource + "`nvoid X() { NvAPI_DRS_DestroySession(s); }"; List = $goodList + "NvAPI_DRS_DestroySession`n"; Expect = 0 },
        @{ Name = 'the chipset read, a noun the verb rule would refuse, passes by name'; Source = $goodSource + "`nvoid X() { NvAPI_SYS_GetChipSetInfo(&c); }"; List = $goodList + "NvAPI_SYS_GetChipSetInfo`n"; Expect = 0 },
        @{ Name = 'another name that ends in a verb is refused until it is named'; Source = $goodSource + "`nvoid X() { NvAPI_GPU_GetChipReset(&c); }"; List = $goodList + "NvAPI_GPU_GetChipReset`n"; Expect = 1 },
        @{ Name = 'a getter whose noun only starts like a verb passes'; Source = $goodSource + "`nvoid X() { NvAPI_DRS_EnumSettings(s, p, 0, &n, &v); NvAPI_GPU_GetAllClockFrequencies(g, &c); }"; List = $goodList + "NvAPI_DRS_EnumSettings`nNvAPI_GPU_GetAllClockFrequencies`n"; Expect = 0 },
        @{ Name = 'a name listed twice FAILS'; Source = $goodSource; List = $goodList + "NvAPI_Initialize`n"; Expect = 1 },
        @{ Name = 'a line that is not one name FAILS'; Source = $goodSource; List = $goodList + "NvAPI_Initialize  # the library`n"; Expect = 1 },
        @{ Name = 'a source with no call at all FAILS'; Source = '// NvAPI_Initialize() is somewhere else'; List = "# nothing`n"; Expect = 1 }
    )
    foreach ($c in $cases) {
        $problems = Test-Bridge $c.Source $c.List
        $ok = $problems.Count -eq $c.Expect
        Write-Host ("  {0,-4} {1}{2}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name, ($(if (-not $ok) { " -> $($problems.Count): $($problems -join '; ')" } else { '' }))) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
        if (-not $ok) { $failed++ }
    }
    if ($failed -gt 0) { Write-Host "nvapi-readonly-check self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host "nvapi-readonly-check self-test: $($cases.Count) cases, both directions" -ForegroundColor Green
    exit 0
}

$sourcePath = Join-Path $RepoRoot 'src/native/FrameLedger.NvapiBridge/src/fl_nvapi_bridge.cpp'
$listPath = Join-Path $RepoRoot 'tools/nvapi-allowed-calls.txt'
foreach ($path in $sourcePath, $listPath) {
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "NVAPI READ-ONLY CHECK FAILED: $path is missing" -ForegroundColor Red; exit 1 }
}
$problems = Test-Bridge (Get-Content -LiteralPath $sourcePath -Raw) (Get-Content -LiteralPath $listPath -Raw)
if ($problems.Count -gt 0) {
    Write-Host 'NVAPI READ-ONLY CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}
$count = (Get-NvapiCall (Get-Content -LiteralPath $sourcePath -Raw)).Count
Write-Host "nvapi-readonly-check OK - the bridge calls $count NVAPI entry points, each on the list, none a writer or an identifier" -ForegroundColor Green
exit 0
