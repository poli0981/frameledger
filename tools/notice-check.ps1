# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Every first-party source file carries the two-line licence header that points at NOTICE, and this gate fails
    when one does not.

.DESCRIPTION
    beta.12 (owner decision D45, 2026-10-03): FrameLedger's own material is GPL-3.0-only WITH additional terms under
    section 7 of the GPL (NOTICE: (b) preserve the notices, (c) mark modified versions, (e) no trademark rights in
    the name). Section 7 says where such terms must be stated: "you must place, in the relevant source files, a
    statement of the additional terms that apply to those files, or a notice indicating where to find the applicable
    terms." A NOTICE file alone at the root is not that, so every first-party source file opens with

        SPDX-License-Identifier: GPL-3.0-only
        Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

    in its own comment syntax: `//` for C# and C++ (.cs .cpp .h .inl), `#` for PowerShell, `<!-- -->` for XAML
    (after an XML declaration when the file has one). The second line may name another copyright holder and year
    (`Copyright (C) <year> <holder> - additional terms under GPLv3 section 7: see NOTICE`, beta.12): a file a
    contributor wrote is theirs, under the same licence and the same additional terms (CONTRIBUTING.md). The set is the tracked files with those extensions, minus
    src/native/third_party/ (other people's code under their own licences). Generated files are generated WITH the
    header (tools/resx-gen.ps1), so a regenerated file passes as written.

    -Fix inserts a missing header (and a blank line after it), keeping the file's own line endings and the rest of
    its bytes exactly; a file whose first line already names an SPDX identifier but whose header differs is
    rewritten in place of those lines. -SelfTest proves the gate in both directions on a temporary tree.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Fix,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SpdxLine = 'SPDX-License-Identifier: GPL-3.0-only'
$script:NoticeLine = 'Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE'
# Any holder and year in the same form: a contributor's own file names its own copyright (CONTRIBUTING.md).
$script:NoticePattern = '^Copyright \(C\) \d{4}(-\d{4})? \S.* - additional terms under GPLv3 section 7: see NOTICE$'
$script:Extensions = @('.cs', '.cpp', '.h', '.inl', '.ps1', '.xaml')
$script:Excluded = @('src/native/third_party/')

function Get-HeaderLines([string]$Extension) {
    switch ($Extension) {
        '.ps1' { return @("# $script:SpdxLine", "# $script:NoticeLine") }
        '.xaml' { return @("<!-- $script:SpdxLine -->", "<!-- $script:NoticeLine -->") }
        default { return @("// $script:SpdxLine", "// $script:NoticeLine") }
    }
}

# Repository-relative, forward-slashed paths of the files the rule covers.
function Get-CoveredFiles([string]$Root) {
    $rels = if (Test-Path (Join-Path $Root '.git')) {
        # Tracked files and new ones not yet added (but never ignored ones), so -Fix also covers a file this branch created.
        @(& git -C $Root ls-files --cached --others --exclude-standard -- @($script:Extensions | ForEach-Object { "*$_" }))
        if ($LASTEXITCODE -ne 0) { throw "notice-check: git ls-files failed in $Root" }
    }
    else {
        # The self-test's temporary tree is not a repository: walk it, skipping build output.
        $rootFull = (Resolve-Path $Root).Path.TrimEnd('\', '/')
        @(Get-ChildItem $Root -Recurse -File | Where-Object { $script:Extensions -contains $_.Extension.ToLowerInvariant() } |
            ForEach-Object { $_.FullName.Substring($rootFull.Length + 1).Replace('\', '/') } |
            Where-Object { $_ -notmatch '(^|/)(bin|obj)/' })
    }
    return @($rels | Where-Object {
            $rel = $_
            $script:Extensions -contains [IO.Path]::GetExtension($rel).ToLowerInvariant() -and
            -not ($script:Excluded | Where-Object { $rel.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) })
        })
}

# Splits a file into (preamble kept above the header, body). The preamble is a UTF-8 BOM and, for XAML, an XML
# declaration line — the only things allowed before the header.
function Split-Preamble([byte[]]$Bytes, [string]$Extension) {
    $bom = $Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF
    $text = [Text.Encoding]::UTF8.GetString($Bytes, ($bom ? 3 : 0), $Bytes.Length - ($bom ? 3 : 0))
    $declaration = ''
    if ($Extension -eq '.xaml' -and $text.StartsWith('<?xml', [StringComparison]::Ordinal)) {
        $end = $text.IndexOf("`n")
        $declaration = $end -lt 0 ? $text : $text.Substring(0, $end + 1)
        $text = $end -lt 0 ? '' : $text.Substring($end + 1)
    }
    return [pscustomobject]@{ Bom = $bom; Declaration = $declaration; Body = $text }
}

# The second header line in its comment syntax, with any holder and year (the pattern above).
function Test-NoticeLine([string]$Line, [string]$Extension) {
    $inner = switch ($Extension) {
        '.ps1' { if ($Line.StartsWith('# ', [StringComparison]::Ordinal)) { $Line.Substring(2) } else { $null } }
        '.xaml' { if ($Line.StartsWith('<!-- ', [StringComparison]::Ordinal) -and $Line.EndsWith(' -->', [StringComparison]::Ordinal)) { $Line.Substring(5, $Line.Length - 9) } else { $null } }
        default { if ($Line.StartsWith('// ', [StringComparison]::Ordinal)) { $Line.Substring(3) } else { $null } }
    }
    return $null -ne $inner -and $inner -cmatch $script:NoticePattern
}

function Test-File([string]$Path, [string]$Extension) {
    $parts = Split-Preamble ([IO.File]::ReadAllBytes($Path)) $Extension
    $lines = $parts.Body -split "`r?`n", 3
    $want = Get-HeaderLines $Extension
    if ($lines.Count -ge 2 -and $lines[0] -ceq $want[0] -and (Test-NoticeLine $lines[1] $Extension)) { return $null }
    if ($lines.Count -ge 1 -and $lines[0] -match 'SPDX-License-Identifier') { return 'differs' }
    return 'missing'
}

function Repair-File([string]$Path, [string]$Extension, [string]$State) {
    $parts = Split-Preamble ([IO.File]::ReadAllBytes($Path)) $Extension
    $eol = $parts.Body.Contains("`r`n") -or $parts.Declaration.Contains("`r`n") -or -not $parts.Body.Contains("`n") ? "`r`n" : "`n"
    $body = $parts.Body
    if ($State -eq 'differs') {
        # Drop the old header lines (every leading line that names SPDX or the NOTICE pointer) and one blank line.
        $rest = $body
        while ($true) {
            $line = [regex]::Match($rest, '^([^\r\n]*)(\r?\n)')
            if (-not $line.Success -or $line.Groups[1].Value -notmatch 'SPDX-License-Identifier|see NOTICE') { break }
            $rest = $rest.Substring($line.Length)
        }
        $blank = [regex]::Match($rest, '^\r?\n')
        if ($blank.Success) { $rest = $rest.Substring($blank.Length) }
        $body = $rest
    }
    $header = ((Get-HeaderLines $Extension) -join $eol) + $eol + $eol
    $text = $parts.Declaration + $header + $body
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    if ($parts.Bom) { $bytes = [byte[]](0xEF, 0xBB, 0xBF) + $bytes }
    [IO.File]::WriteAllBytes($Path, $bytes)
}

function Invoke-Check([string]$Root, [bool]$Repair) {
    $problems = [Collections.Generic.List[string]]::new()
    $fixed = 0
    foreach ($rel in Get-CoveredFiles $Root) {
        $path = Join-Path $Root $rel
        $ext = [IO.Path]::GetExtension($rel).ToLowerInvariant()
        $state = Test-File $path $ext
        if ($null -eq $state) { continue }
        if ($Repair) {
            Repair-File $path $ext $state
            $fixed++
            if ($null -ne (Test-File $path $ext)) { $problems.Add("$rel - the header could not be written") }
        }
        else {
            $problems.Add("$rel - header $state")
        }
    }
    return [pscustomobject]@{ Problems = $problems; Fixed = $fixed }
}

if ($SelfTest) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('fl-notice-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Force -Path (Join-Path $tmp 'src/native/third_party/x'), (Join-Path $tmp 'src/App'), (Join-Path $tmp 'tools') | Out-Null
        $crlf = "`r`n"
        $good = ((Get-HeaderLines '.cs') -join $crlf) + $crlf + $crlf + "namespace A;$crlf"
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/Good.cs'), $good)
        [IO.File]::WriteAllText((Join-Path $tmp 'src/native/third_party/x/vendor.h'), "#pragma once$crlf")
        $cases = 0

        # 1. A compliant tree passes, and a third-party file without a header is not looked at.
        $r = Invoke-Check $tmp $false
        if ($r.Problems.Count -ne 0) { throw "self-test: a compliant tree should pass, got: $($r.Problems -join '; ')" }
        $cases++

        # 2. A C# file without the header fails, naming the file.
        $bareCs = "using System;$crlf${crlf}namespace A;$crlf"
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/Bare.cs'), $bareCs)
        $r = Invoke-Check $tmp $false
        if ($r.Problems.Count -ne 1 -or $r.Problems[0] -notmatch 'Bare\.cs - header missing') { throw "self-test: a bare .cs should fail naming it, got: $($r.Problems -join '; ')" }
        $cases++

        # 3. -Fix writes it, keeps CRLF and every original byte after the header; the check then passes.
        $r = Invoke-Check $tmp $true
        if ($r.Fixed -ne 1 -or $r.Problems.Count -ne 0) { throw "self-test: -Fix should repair one file, got fixed=$($r.Fixed) problems=$($r.Problems -join '; ')" }
        $after = [IO.File]::ReadAllText((Join-Path $tmp 'src/App/Bare.cs'))
        $expected = ((Get-HeaderLines '.cs') -join $crlf) + $crlf + $crlf + $bareCs
        if ($after -cne $expected) { throw 'self-test: -Fix must prepend the header and keep the rest byte for byte' }
        if ((Invoke-Check $tmp $false).Problems.Count -ne 0) { throw 'self-test: a repaired tree should pass' }
        $cases++

        # 4. XAML: the header goes after an XML declaration, as comments, and the file is still XML.
        $xaml = "<?xml version=""1.0"" encoding=""utf-8""?>$crlf<UserControl xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" />$crlf"
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/View.xaml'), $xaml)
        $null = Invoke-Check $tmp $true
        $fixedXaml = [IO.File]::ReadAllText((Join-Path $tmp 'src/App/View.xaml'))
        if (-not $fixedXaml.StartsWith('<?xml', [StringComparison]::Ordinal) -or $fixedXaml -notmatch "<!-- SPDX-License-Identifier: GPL-3\.0-only -->") { throw 'self-test: a XAML header must follow the declaration' }
        $null = [xml]$fixedXaml
        $cases++

        # 5. PowerShell: the header goes above #Requires and comment-based help, and the script still parses.
        $ps = "#Requires -Version 7.0$crlf<#$crlf.SYNOPSIS$crlf    x$crlf#>$crlf" + 'param()' + "$crlf'ok'$crlf"
        [IO.File]::WriteAllText((Join-Path $tmp 'tools/t.ps1'), $ps)
        $null = Invoke-Check $tmp $true
        $tokens = $null; $errors = $null
        $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $tmp 'tools/t.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count -ne 0) { throw "self-test: a repaired script must still parse, got: $($errors -join '; ')" }
        $cases++

        # 6. A header that names SPDX but says something else is red, and -Fix replaces it instead of stacking a second.
        $stale = "// SPDX-License-Identifier: GPL-3.0-or-later$crlf// old line, see NOTICE$crlf${crlf}namespace B;$crlf"
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/Stale.cs'), $stale)
        $r = Invoke-Check $tmp $false
        if ($r.Problems.Count -ne 1 -or $r.Problems[0] -notmatch 'Stale\.cs - header differs') { throw "self-test: a stale header should fail as 'differs', got: $($r.Problems -join '; ')" }
        $null = Invoke-Check $tmp $true
        $fixedStale = [IO.File]::ReadAllText((Join-Path $tmp 'src/App/Stale.cs'))
        if ($fixedStale -cne (((Get-HeaderLines '.cs') -join $crlf) + $crlf + $crlf + "namespace B;$crlf")) { throw 'self-test: a stale header must be replaced, not stacked' }
        $cases++

        # 7. A contributor's own copyright line in the same form passes; one that drops the NOTICE pointer does not.
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/Theirs.cs'), "// $script:SpdxLine$crlf// Copyright (C) 2027 Jane Doe - additional terms under GPLv3 section 7: see NOTICE$crlf${crlf}namespace C;$crlf")
        [IO.File]::WriteAllText((Join-Path $tmp 'src/App/NoPointer.cs'), "// $script:SpdxLine$crlf// Copyright (C) 2027 Jane Doe$crlf${crlf}namespace D;$crlf")
        $r = Invoke-Check $tmp $false
        if ($r.Problems.Count -ne 1 -or $r.Problems[0] -notmatch 'NoPointer\.cs - header') { throw "self-test: another holder's line should pass and a line without the NOTICE pointer fail, got: $($r.Problems -join '; ')" }
        Remove-Item (Join-Path $tmp 'src/App/NoPointer.cs')
        $cases++

        Write-Host "notice-check self-test OK - $cases cases, both directions"
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
    exit 0
}

$result = Invoke-Check $RepoRoot $Fix.IsPresent
if ($Fix) {
    Write-Host "notice-check: $($result.Fixed) file(s) given the header"
}
if ($result.Problems.Count -ne 0) {
    Write-Host 'NOTICE CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $result.Problems) { Write-Host "  $p" -ForegroundColor Red }
    Write-Host '  Every first-party source file opens with the SPDX line and the NOTICE pointer (GPLv3 section 7 wants the' -ForegroundColor Red
    Write-Host '  additional terms named "in the relevant source files"). Run: ./tools/notice-check.ps1 -Fix' -ForegroundColor Red
    exit 1
}
$count = @(Get-CoveredFiles $RepoRoot).Count
Write-Host "notice-check OK - $count first-party source file(s) carry the SPDX line and the NOTICE pointer"
exit 0
