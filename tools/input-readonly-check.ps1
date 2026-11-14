# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    What FrameLedger asks Windows about controllers, the keyboard and the mouse is a committed list of calls, none of
    which sends anything to a device or reads a key.

.DESCRIPTION
    docs/21_INPUT_DEVICES.md makes three promises a reviewer would otherwise have to check by reading (owner decisions
    D155-D163, 2026-11-14):

      - keyboard and mouse are known only by the time of the last input: no key, no button, no position is ever read,
        and no hook is installed (D158);
      - nothing is ever sent to a controller: no output report, no feature-report request, no vibration (D159);
      - controllers are read by the Agent from outside the game: the code loaded into a game reads no input (D155).

    The legal texts say the same to the user. A sentence does not keep itself true; this does.

    WHAT IT HOLDS:

      1. every Windows function declared in src/FrameLedger.Infrastructure/Input/Win32Input.cs - the one file the
         input reader's calls are declared in - is on tools/input-allowed-calls.txt, and every name there is declared
         (a list longer than the code is a list nobody reads);
      2. no name on the list is one that writes to a device, injects input or reads a key - whatever the list says.
         This rule judges the LIST, so the way to let such a call in is not "add a line to the text file";
      3. no managed source outside src/FrameLedger.Infrastructure/Input/ names an input function at all, and none
         anywhere - nor NativeMethods.txt, where CsWin32 would be asked for one - names a forbidden one;
      4. raw input is registered for joysticks and gamepads only: InputReader.RegisteredUsages is exactly usage page 1,
         usages 4 and 5, and the registration is built from it and from nothing else;
      5. no native source (src/native, the vendored code aside) names an input function: nothing loaded into a game,
         and no other native binary, reads input.

    WHAT IT DOES NOT DO. It reads names, not behaviour: it cannot tell what a call is used for. It does not look at
    the tests, which may drive a real keyboard to test the App's own shortcuts. Comments and string literals are taken
    out first, so a function named in the comment that says it is never called is not a call.

    -SelfTest proves every rule in both directions on in-memory sources.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The names of Windows' input functions, as tokens. XInput's functions are XInputGet*, XInputSet* and XInputEnable;
# the project's own types are XInputReading, XInputPad and the like, and are not these.
$script:InputCall = '\b(XInput(?:Get|Set|Enable)[A-Za-z]*|GetRawInput[A-Za-z]*|RegisterRawInputDevices|GetRegisteredRawInputDevices|GetLastInputInfo|HidD_\w+|HidP_\w+|SendInput|keybd_event|mouse_event|GetAsyncKeyState|GetKeyState|GetKeyboardState|GetKeyNameText[AW]?|MapVirtualKey(?:Ex)?[AW]?|ToUnicode(?:Ex)?|SetWindowsHookEx[AW]?|BlockInput|DirectInput8Create|GameInputCreate)\b'

# A namespace is not a call, and is refused all the same: its readings stop when the program is not in front, and it
# is not on the list of what was decided (21_INPUT_DEVICES section Rejected).
$script:InputNamespace = 'Windows\.Gaming\.Input'

# What may never be called, on the list or off it. Each with its reason, because a list judged by a pattern nobody
# can read is a list nobody trusts.
$script:Forbidden = [ordered]@{
    '^XInputSet'                 = 'sends to a controller (D159)'
    '^XInputEnable$'             = 'changes what every XInput call of the process returns'
    '^XInputGetKeystroke$'       = 'reads which button was pressed, as a key (D158, 21_INPUT_DEVICES section What is never read)'
    '^XInputGetAudioDeviceIds$'  = 'identifies the audio devices of a headset'
    '^XInputGetDSoundAudioDeviceGuids$' = 'identifies the audio devices of a headset'
    '^HidD_'                     = 'opens or talks to a HID device: an output report, a feature report, a serial number (D159)'
    '^HidP_'                     = 'not decided: 21_INPUT_DEVICES reads a DualSense by its own layout and no other HID controller'
    '^SendInput$'                = 'injects input'
    '^keybd_event$'              = 'injects input'
    '^mouse_event$'              = 'injects input'
    '^BlockInput$'               = 'blocks input'
    '^GetAsyncKeyState$'         = 'reads a key (D158)'
    '^GetKeyState$'              = 'reads a key (D158)'
    '^GetKeyboardState$'         = 'reads every key (D158)'
    '^GetKeyNameText'            = 'names a key (D158)'
    '^MapVirtualKey'             = 'translates a key (D158)'
    '^ToUnicode'                 = 'turns keys into text (D158)'
    '^SetWindowsHookEx'          = 'installs a hook (D158; tools/chokepoint-check.ps1 refuses it as well)'
    '^GetRegisteredRawInputDevices$' = 'not needed: the reader knows what it registered'
    '^DirectInput8Create$'       = 'DirectInput: not decided, and it does not read in the background'
    '^GameInputCreate$'          = 'GameInput: rejected (21_INPUT_DEVICES section Rejected)'
}

# Source text without its comments and string literals.
function Get-Code([string]$Source) {
    $code = [regex]::Replace($Source, '/\*.*?\*/', ' ', 'Singleline')
    $code = [regex]::Replace($code, '//[^\r\n]*', ' ')
    $code = [regex]::Replace($code, '@"(""|[^"])*"', '""')
    return [regex]::Replace($code, '"(\\.|[^"\\\r\n])*"', '""')
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
        if ($line -cnotmatch '^[A-Za-z_]\w*$') { $problems.Add("the list's line $number is not one function name: '$line'"); continue }
        if ($names.Contains($line)) { $problems.Add("the list names $line twice"); continue }
        $names.Add($line)
    }
    return @{ Names = @($names); Problems = @($problems) }
}

function Get-ForbiddenReason([string]$Name) {
    foreach ($pattern in $script:Forbidden.Keys) {
        if ($Name -cmatch $pattern) { return $script:Forbidden[$pattern] }
    }
    return $null
}

# Every rule, on text. $Declarations is Win32Input.cs; $Managed maps a repository path to a managed source (the
# declarations' own file among them); $NativeMethods is CsWin32's list; $Reader is InputReader.cs; $Backend is
# Win32InputBackend.cs; $Native maps a path to a native source.
function Test-Input([string]$Declarations, [string]$ListText, [hashtable]$Managed, [string]$NativeMethods, [string]$Reader, [string]$Backend, [hashtable]$Native) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $list = Read-AllowedList $ListText
    foreach ($p in $list.Problems) { $problems.Add($p) }
    $allowed = $list.Names

    # 1. The declarations and the list say the same.
    $declared = @([regex]::Matches((Get-Code $Declarations), '\bstatic\s+extern\s+[^;(]*?\b([A-Za-z_]\w*)\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    if ($declared.Count -eq 0) { $problems.Add('the declarations file declares no function: the check would hold nothing') }
    foreach ($name in $declared) {
        if ($allowed -cnotcontains $name) { $problems.Add("$name is declared and is not on the list") }
    }
    foreach ($name in $allowed) {
        if ($declared -cnotcontains $name) { $problems.Add("$name is on the list and is not declared: a list longer than the code is a list nobody reads") }
    }

    # 2. The list itself.
    foreach ($name in $allowed) {
        $why = Get-ForbiddenReason $name
        if ($why) { $problems.Add("$name may not be on the list: it $why") }
    }

    # 3. The managed sources, and what CsWin32 is asked for.
    foreach ($path in ($Managed.Keys | Sort-Object)) {
        $code = Get-Code $Managed[$path]
        $inReader = $path -like 'src/FrameLedger.Infrastructure/Input/*'
        foreach ($m in [regex]::Matches($code, $script:InputCall)) {
            $name = $m.Groups[1].Value
            $why = Get-ForbiddenReason $name
            if ($why) { $problems.Add("$path names ${name}: it $why") }
            elseif (-not $inReader) { $problems.Add("$path names ${name}: input is read in src/FrameLedger.Infrastructure/Input/ and nowhere else") }
            elseif ($allowed -cnotcontains $name) { $problems.Add("$path names ${name}, which is not on the list") }
        }
        if ($code -cmatch $script:InputNamespace) { $problems.Add("$path names Windows.Gaming.Input: rejected (21_INPUT_DEVICES section Rejected)") }
    }
    foreach ($raw in ($NativeMethods -split "`r?`n")) {
        $line = $raw.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('//')) { continue }
        if ($line -cmatch ('^' + $script:InputCall.Trim('\', 'b') + '$') -or $line -cmatch $script:InputCall) {
            $problems.Add("NativeMethods.txt asks CsWin32 for ${line}: the input reader's calls are declared in Win32Input.cs, where this check reads them")
        }
    }

    # 4. What raw input is registered for.
    $readerCode = Get-Code $Reader
    if ($readerCode -cnotmatch 'RegisteredUsages\s*\{\s*get;\s*\}\s*=\s*\[\s*\(\s*1\s*,\s*4\s*\)\s*,\s*\(\s*1\s*,\s*5\s*\)\s*\]\s*;') {
        $problems.Add('InputReader.RegisteredUsages is not exactly [(1, 4), (1, 5)]: raw input is registered for joysticks and gamepads only - usage 2 is a mouse and 6 a keyboard (D158)')
    }
    $backendCode = Get-Code $Backend
    if ($backendCode -cnotmatch 'InputReader\.RegisteredUsages') { $problems.Add('the backend does not build its registration from InputReader.RegisteredUsages') }
    if ($backendCode -cmatch '\bUsage(Page)?\s*=\s*(0x[0-9A-Fa-f]+|\d)') { $problems.Add('the backend registers a usage written as a number: the registration is InputReader.RegisteredUsages and nothing else') }

    # 5. The native sources.
    foreach ($path in ($Native.Keys | Sort-Object)) {
        $code = Get-Code $Native[$path]
        foreach ($m in [regex]::Matches($code, $script:InputCall)) {
            $problems.Add("$path names $($m.Groups[1].Value): no native code reads input - controllers are read by the Agent, in C#, from outside the game (D155)")
        }
    }

    return @($problems)
}

if ($SelfTest) {
    $goodDeclarations = @'
internal static unsafe class Win32Input
{
    // XInputSetState is never declared here.
    [DllImport("user32.dll")] public static extern int RegisterRawInputDevices(RawInputDevice* devices, uint count, uint size);
    [DllImport("user32.dll")] public static extern uint GetRawInputBuffer(void* data, uint* size, uint headerSize);
    [DllImport("user32.dll")] public static extern int GetLastInputInfo(LastInputInfo* info);
    [DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint userIndex, XInputState* state);
    public struct XInputState { public uint PacketNumber; }
}
'@
    $goodList = "# the reader's calls`nRegisterRawInputDevices`nGetRawInputBuffer`nGetLastInputInfo`nXInputGetState`n"
    $goodReader = 'public static IReadOnlyList<(ushort UsagePage, ushort Usage)> RegisteredUsages { get; } = [(1, 4), (1, 5)];'
    $goodBackend = 'IReadOnlyList<(ushort UsagePage, ushort Usage)> usages = InputReader.RegisteredUsages; devices[i] = new Win32Input.RawInputDevice { UsagePage = usages[i].UsagePage, Usage = usages[i].Usage, Flags = flags }; Win32Input.RegisterRawInputDevices(devices, 2, 16); Win32Input.XInputGetState(0, &state);'
    $goodManaged = @{
        'src/FrameLedger.Infrastructure/Input/Win32Input.cs'        = $goodDeclarations
        'src/FrameLedger.Infrastructure/Input/Win32InputBackend.cs' = $goodBackend
        'src/FrameLedger.Application/Input/InputReader.cs'          = $goodReader + ' public bool XInputAvailable => true; XInputReading r; // GetAsyncKeyState is never called'
        'src/FrameLedger.App/Charts/ChartWheel.cs'                  = '/// <c>GetKeyState</c> reported the machine''s NumLock.' + "`n" + 'var s = "SendInput"; Keyboard.GetKeyStates(Key.A);'
    }
    $goodNativeMethods = "// the window`nIsWindow`nGetWindowThreadProcessId`n"
    $goodNative = @{ 'src/native/FrameLedger.Overlay/src/dllmain.cpp' = '// XInputGetState is the Agent''s to call' + "`n" + 'void Hook() {}' }

    function New-Case([string]$Name, [int]$Expect, [hashtable]$Change = @{}) {
        $managed = @{}
        foreach ($k in $goodManaged.Keys) { $managed[$k] = $goodManaged[$k] }
        if ($Change.ContainsKey('Managed')) { foreach ($k in $Change.Managed.Keys) { $managed[$k] = $Change.Managed[$k] } }
        $backend = if ($Change.ContainsKey('Backend')) { $Change.Backend } else { $goodBackend }
        if ($Change.ContainsKey('Backend')) { $managed['src/FrameLedger.Infrastructure/Input/Win32InputBackend.cs'] = $backend }
        return @{
            Name          = $Name
            Expect        = $Expect
            Declarations  = if ($Change.ContainsKey('Declarations')) { $Change.Declarations } else { $goodDeclarations }
            List          = if ($Change.ContainsKey('List')) { $Change.List } else { $goodList }
            Managed       = $managed
            NativeMethods = if ($Change.ContainsKey('NativeMethods')) { $Change.NativeMethods } else { $goodNativeMethods }
            Reader        = if ($Change.ContainsKey('Reader')) { $Change.Reader } else { $goodReader }
            Backend       = $backend
            Native        = if ($Change.ContainsKey('Native')) { $Change.Native } else { $goodNative }
        }
    }

    $setter = $goodDeclarations.Replace('public struct XInputState', '[DllImport("xinput1_4.dll")] public static extern uint XInputSetState(uint userIndex, void* vibration);' + "`n    public struct XInputState")
    $cases = @(
        (New-Case 'the reader as it is, with the project''s own XInput types and a key function named in comments, passes' 0),
        (New-Case 'a declared function that is not on the list FAILS, and so does each source that names it' 3 @{ List = "RegisterRawInputDevices`nGetRawInputBuffer`nGetLastInputInfo`n" }),
        (New-Case 'a name on the list that is not declared FAILS' 1 @{ List = $goodList + "GetRawInputDeviceList`n" }),
        (New-Case 'a line of the list that is not one name FAILS, and the same name twice' 2 @{ List = $goodList + "Get Raw Input`nXInputGetState`n" }),
        (New-Case 'XInputSetState declared and put on the list FAILS twice: the list may not hold it, and the source may not name it' 2 @{ Declarations = $setter; List = $goodList + "XInputSetState`n"; Managed = @{ 'src/FrameLedger.Infrastructure/Input/Win32Input.cs' = $setter } }),
        (New-Case 'a declarations file that declares nothing FAILS, with every name of the list then undeclared' 5 @{ Declarations = '// nothing' }),
        (New-Case 'a key read anywhere in the managed sources FAILS' 1 @{ Managed = @{ 'src/FrameLedger.App/Services/Shortcut.cs' = 'if (GetAsyncKeyState(0x41) != 0) { }' } }),
        (New-Case 'a HID feature report in the reader FAILS' 1 @{ Backend = $goodBackend + ' HidD_GetFeature(handle, buffer, 41);' }),
        (New-Case 'an allowed call made outside the reader''s folder FAILS' 1 @{ Managed = @{ 'src/FrameLedger.Agent/Program.cs' = 'XInputGetState(0, &state);' } }),
        (New-Case 'a call in the reader''s folder that is not on the list FAILS' 1 @{ Backend = $goodBackend + ' Win32Input.GetRawInputDeviceList(null, &n, 16);' }),
        (New-Case 'Windows.Gaming.Input named in a source FAILS' 1 @{ Managed = @{ 'src/FrameLedger.Infrastructure/Input/Wgi.cs' = 'using Windows.Gaming.Input;' } }),
        (New-Case 'an input function asked of CsWin32 FAILS' 1 @{ NativeMethods = $goodNativeMethods + "XInputGetState`n" }),
        (New-Case 'a keyboard added to what raw input is registered for FAILS' 1 @{ Reader = $goodReader.Replace('(1, 5)]', '(1, 5), (1, 6)]') }),
        (New-Case 'a backend that registers a usage written as a number FAILS' 1 @{ Backend = $goodBackend.Replace('Usage = usages[i].Usage', 'Usage = 6') }),
        (New-Case 'a backend that does not use the registered usages FAILS' 1 @{ Backend = 'devices[i] = new Win32Input.RawInputDevice { UsagePage = page, Usage = usage };' }),
        (New-Case 'an input function in the code loaded into a game FAILS' 1 @{ Native = @{ 'src/native/FrameLedger.Overlay/src/dllmain.cpp' = 'XINPUT_STATE s; XInputGetState(0, &s);' } }),
        (New-Case 'a hook named in a native source FAILS' 1 @{ Native = @{ 'src/native/FrameLedger.Injector/src/injector.cpp' = 'SetWindowsHookExW(WH_KEYBOARD_LL, proc, nullptr, 0);' } })
    )
    $failed = 0
    foreach ($c in $cases) {
        $problems = @(Test-Input $c.Declarations $c.List $c.Managed $c.NativeMethods $c.Reader $c.Backend $c.Native)
        $ok = $problems.Count -eq $c.Expect
        Write-Host ("  {0,-4} {1}{2}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name, ($(if (-not $ok) { " -> $($problems.Count): $($problems -join '; ')" } else { '' })))
        if (-not $ok) { $failed++ }
    }
    if ($failed -gt 0) { Write-Host "input-readonly-check self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host "input-readonly-check self-test: $($cases.Count) cases, both directions" -ForegroundColor Green
    exit 0
}

$declarationsPath = Join-Path $RepoRoot 'src/FrameLedger.Infrastructure/Input/Win32Input.cs'
$backendPath = Join-Path $RepoRoot 'src/FrameLedger.Infrastructure/Input/Win32InputBackend.cs'
$readerPath = Join-Path $RepoRoot 'src/FrameLedger.Application/Input/InputReader.cs'
$listPath = Join-Path $RepoRoot 'tools/input-allowed-calls.txt'
$nativeMethodsPath = Join-Path $RepoRoot 'src/FrameLedger.Infrastructure/NativeMethods.txt'
foreach ($path in $declarationsPath, $backendPath, $readerPath, $listPath, $nativeMethodsPath) {
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "INPUT READ-ONLY CHECK FAILED: $path is missing" -ForegroundColor Red; exit 1 }
}

$skip = [regex]'[\\/](bin|obj)[\\/]'
$managed = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'src') -Recurse -File -Filter '*.cs') {
    if ($skip.IsMatch($file.FullName) -or $file.FullName -like '*\native\*') { continue }
    $managed[[IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace('\', '/')] = Get-Content -LiteralPath $file.FullName -Raw
}
$native = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'src/native') -Recurse -File -Include '*.cpp', '*.h', '*.inl', '*.c', '*.hpp') {
    if ($file.FullName -like '*\third_party\*') { continue }
    $native[[IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace('\', '/')] = Get-Content -LiteralPath $file.FullName -Raw
}
if ($managed.Count -eq 0 -or $native.Count -eq 0) { Write-Host 'INPUT READ-ONLY CHECK FAILED: no managed or no native source was found' -ForegroundColor Red; exit 1 }

$listText = Get-Content -LiteralPath $listPath -Raw
$problems = @(Test-Input (Get-Content -LiteralPath $declarationsPath -Raw) $listText $managed (Get-Content -LiteralPath $nativeMethodsPath -Raw) (Get-Content -LiteralPath $readerPath -Raw) (Get-Content -LiteralPath $backendPath -Raw) $native)
if ($problems.Count -gt 0) {
    Write-Host 'INPUT READ-ONLY CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}
$count = (Read-AllowedList $listText).Names.Count
Write-Host "input-readonly-check OK - the input reader declares $count Windows functions, each on the list; none sends to a device, injects input or reads a key; no input function in $($managed.Count) managed sources outside the reader, nor in $($native.Count) native sources" -ForegroundColor Green
exit 0
