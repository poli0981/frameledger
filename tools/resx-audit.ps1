#Requires -Version 7.0
<#
.SYNOPSIS
    The resx gate docs/09_I18N.md promised: every key in every language, safety strings reviewed as legal
    text, and the generated accessor current.

.DESCRIPTION
    09_I18N §Translation workflow: "CI check (tools/resx-audit): every key present in all three files".
    This script did not exist for the first fourteen months the document named it; build.ps1 skipped the
    step loudly, and 13_CI_CD and CLAUDE.md both recorded the absence. It exists as of P3 PR-2 (2026-09-13),
    with the first .resx family.

    Per FAMILY (a neutral Strings.resx and its Strings.<culture>.resx beside it, under src/):
      1. every key is a C# identifier, unique, and has a non-empty neutral value;
      2. every required culture file exists and carries EXACTLY the neutral key set — a missing key would
         silently fall back to English, an extra one would be dead text nobody can reach;
      3. 09_I18N §Safety-critical strings: a `Safety_*` key in `ja` must carry a comment saying either
         `safety: human review required` — and then its VALUE MUST BE THE ENGLISH VERBATIM, which is how
         "shown as en until signed" is true by construction rather than by a runtime fallback the key-set
         rule (2) forbids — or `safety: reviewed by <name>`; a machine-drafted ban warning with neither is
         the harm that section names, and it is red here;
      4. the committed Strings.Designer.cs is what tools/resx-gen.ps1 would write (the accessor is
         generated and committed; drift is the silent failure mode of "generated").

    FAIL-CLOSED WHEN NOTHING IS FOUND: no family under src/ means the gate would be vacuous, so it is red
    rather than green — the same rule tools/package-closure-check.ps1 applies to a closure of two.

.PARAMETER SelfTest
    Runs the decision table in both directions on fixtures in a temp directory; at least four cases must
    go RED for the run to pass, so a check that stopped checking cannot report a clean self-test.
#>
[CmdletBinding(DefaultParameterSetName = 'Check')]
param(
    [Parameter(ParameterSetName = 'Check')]
    [string]$Root = (Split-Path $PSScriptRoot -Parent),

    [Parameter(ParameterSetName = 'Check')]
    [string[]]$Cultures = @('vi', 'ja'),

    [Parameter(ParameterSetName = 'SelfTest', Mandatory = $true)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Gen = Join-Path $PSScriptRoot 'resx-gen.ps1'
$script:ReviewRequired = 'safety: human review required'
$script:ReviewedBy = 'safety: reviewed by '
$script:SafetyPrefix = 'Safety_'
$script:ReviewedCulture = 'ja'

function Read-Resx([string]$path) {
    [xml]$doc = Get-Content -Raw $path
    $entries = @{}
    $order = New-Object System.Collections.Generic.List[string]
    $dups = New-Object System.Collections.Generic.List[string]
    foreach ($d in @($doc.root.data)) {
        if ($null -eq $d) { continue }
        $name = [string]$d.name
        if ($entries.ContainsKey($name)) { $dups.Add($name); continue }
        $value = if ($null -ne $d.value) { [string]$d.value } else { '' }
        $comment = if ($d.PSObject.Properties['comment'] -and $null -ne $d.comment) { [string]$d.comment } else { '' }
        $entries[$name] = @{ Value = $value; Comment = $comment }
        $order.Add($name)
    }
    return @{ Entries = $entries; Order = $order; Duplicates = $dups }
}

<#
    Returns the list of problems for one family; empty means it passes. A list, not a boolean, so the
    live run can print every defect at once rather than the first one per build.
#>
function Test-ResxFamily([string]$neutralPath, [string[]]$cultures) {
    $problems = New-Object System.Collections.Generic.List[string]
    $rel = $neutralPath
    $neutral = Read-Resx $neutralPath
    foreach ($d in $neutral.Duplicates) { $problems.Add("$rel`: key '$d' appears twice") }
    foreach ($k in $neutral.Order) {
        if ($k -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { $problems.Add("$rel`: key '$k' is not a C# identifier (09_I18N: Area_Element_Qualifier)") }
        if ([string]::IsNullOrWhiteSpace($neutral.Entries[$k].Value)) { $problems.Add("$rel`: key '$k' has an empty neutral value") }
    }

    if ($neutral.Order.Count -eq 0) { $problems.Add("$rel`: no keys at all") }

    $dir = Split-Path $neutralPath -Parent
    $base = [IO.Path]::GetFileNameWithoutExtension($neutralPath)
    foreach ($culture in $cultures) {
        $culturePath = Join-Path $dir "$base.$culture.resx"
        if (-not (Test-Path $culturePath)) {
            $problems.Add("$rel`: no $culture file ($base.$culture.resx) — 09_I18N requires en, vi, ja")
            continue
        }

        $translated = Read-Resx $culturePath
        foreach ($d in $translated.Duplicates) { $problems.Add("$culturePath`: key '$d' appears twice") }
        foreach ($k in $neutral.Order) {
            if (-not $translated.Entries.ContainsKey($k)) { $problems.Add("$culturePath`: key '$k' is missing (it would fall back to English silently)") }
        }

        foreach ($k in $translated.Order) {
            if (-not $neutral.Entries.ContainsKey($k)) { $problems.Add("$culturePath`: key '$k' does not exist in the neutral file (dead text)") }
        }

        if ($culture -eq $script:ReviewedCulture) {
            foreach ($k in $neutral.Order) {
                if (-not $k.StartsWith($script:SafetyPrefix, [StringComparison]::Ordinal)) { continue }
                if (-not $translated.Entries.ContainsKey($k)) { continue }
                $comment = $translated.Entries[$k].Comment
                $pending = $comment.Contains($script:ReviewRequired, [StringComparison]::Ordinal)
                $reviewed = $comment.Contains($script:ReviewedBy, [StringComparison]::Ordinal)
                if (-not ($pending -or $reviewed)) {
                    $problems.Add("$culturePath`: safety key '$k' carries neither '$($script:ReviewRequired)' nor '$($script:ReviewedBy)<name>' — 09_I18N §Safety-critical strings: a mistranslated ban warning is a real harm")
                }
                elseif ($pending -and -not $reviewed -and $translated.Entries[$k].Value -cne $neutral.Entries[$k].Value) {
                    $problems.Add("$culturePath`: safety key '$k' is marked '$($script:ReviewRequired)' but carries text other than the English — until a reviewer signs, the value must be the en string verbatim (that is the fallback)")
                }
            }
        }
    }

    if (Test-Path $script:Gen) {
        # The generator throws on a key it cannot name (caught above as a non-identifier) and exits 1 on drift.
        try {
            & $script:Gen -Resx $neutralPath -Check *> $null
            if ($LASTEXITCODE -ne 0) { $problems.Add("$rel`: $base.Designer.cs is missing or stale — run tools/resx-gen.ps1 -Resx $rel") }
        }
        catch {
            $problems.Add("$rel`: $($_.Exception.Message)")
        }
    }
    else {
        $problems.Add("tools/resx-gen.ps1 is missing, so the accessor cannot be checked")
    }

    # Callers wrap this in @(): PowerShell unrolls a returned List, and an empty one becomes $null.
    return $problems
}

function Find-Families([string]$root) {
    $src = Join-Path $root 'src'
    if (-not (Test-Path $src)) { return @() }
    return @(Get-ChildItem $src -Recurse -Filter '*.resx' -File |
            Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
            Where-Object { $_.Name -notmatch '\.[a-z]{2}(-[A-Za-z]{2,4})?\.resx$' })
}

function New-Fixture([string]$dir, [hashtable]$files) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    foreach ($name in $files.Keys) {
        $data = ($files[$name] | ForEach-Object {
                $c = if ($_.ContainsKey('Comment')) { "<comment>$($_.Comment)</comment>" } else { '' }
                "  <data name=""$($_.Key)"" xml:space=""preserve""><value>$($_.Value)</value>$c</data>"
            }) -join "`n"
        $xml = "<?xml version=""1.0"" encoding=""utf-8""?>`n<root>`n  <resheader name=""resmimetype""><value>text/microsoft-resx</value></resheader>`n  <resheader name=""version""><value>2.0</value></resheader>`n$data`n</root>`n"
        [IO.File]::WriteAllText((Join-Path $dir $name), $xml, [Text.UTF8Encoding]::new($false))
    }
}

if ($SelfTest) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("fl-resx-audit-" + [Guid]::NewGuid().ToString('N'))
    try {
        $en = @(@{ Key = 'Nav_Home'; Value = 'Home' }, @{ Key = 'Safety_Warn'; Value = 'May result in a permanent ban.' })
        $vi = @(@{ Key = 'Nav_Home'; Value = 'Trang chủ' }, @{ Key = 'Safety_Warn'; Value = 'Có thể dẫn đến khoá tài khoản vĩnh viễn.' })
        $jaMarked = @(@{ Key = 'Nav_Home'; Value = 'ホーム'; Comment = 'review' }, @{ Key = 'Safety_Warn'; Value = 'May result in a permanent ban.'; Comment = 'safety: human review required' })
        $jaMarkedButTranslated = @(@{ Key = 'Nav_Home'; Value = 'ホーム' }, @{ Key = 'Safety_Warn'; Value = '機械翻訳'; Comment = 'safety: human review required' })
        $jaReviewed = @(@{ Key = 'Nav_Home'; Value = 'ホーム' }, @{ Key = 'Safety_Warn'; Value = '永久BANの可能性があります。'; Comment = 'safety: reviewed by A. Reviewer' })
        $jaUnmarked = @(@{ Key = 'Nav_Home'; Value = 'ホーム' }, @{ Key = 'Safety_Warn'; Value = '機械翻訳' })

        $cases = @(
            @{ Name = 'a complete family with the ja safety key marked for review passes'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaMarked }; Gen = $true; ExpectFailure = $false }
            @{ Name = 'a reviewed ja safety key passes'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaReviewed }; Gen = $true; ExpectFailure = $false }
            @{ Name = 'a ja safety key with no review comment FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaUnmarked }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'a ja safety key marked for review that is not the English verbatim FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaMarkedButTranslated }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'a key missing from vi FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = @($vi[0]); 'Strings.ja.resx' = $jaMarked }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'a key only ja has FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaMarked + @(@{ Key = 'Nav_Extra'; Value = 'x' }) }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'a missing culture file FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'an empty neutral value FAILS'; Files = @{ 'Strings.resx' = @(@{ Key = 'Nav_Home'; Value = '' }); 'Strings.vi.resx' = @(@{ Key = 'Nav_Home'; Value = 'x' }); 'Strings.ja.resx' = @(@{ Key = 'Nav_Home'; Value = 'x' }) }; Gen = $true; ExpectFailure = $true }
            @{ Name = 'a key that is not an identifier FAILS'; Files = @{ 'Strings.resx' = @(@{ Key = 'Nav-Home'; Value = 'x' }); 'Strings.vi.resx' = @(@{ Key = 'Nav-Home'; Value = 'x' }); 'Strings.ja.resx' = @(@{ Key = 'Nav-Home'; Value = 'x' }) }; Gen = $false; ExpectFailure = $true }
            @{ Name = 'a stale Designer FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaMarked }; Gen = $true; Stale = $true; ExpectFailure = $true }
            @{ Name = 'a missing Designer FAILS'; Files = @{ 'Strings.resx' = $en; 'Strings.vi.resx' = $vi; 'Strings.ja.resx' = $jaMarked }; Gen = $false; ExpectFailure = $true }
        )

        $failures = 0
        $reds = 0
        $i = 0
        foreach ($case in $cases) {
            $dir = Join-Path $temp ("case" + ($i++))
            New-Fixture $dir $case.Files
            # No -Namespace: the check side derives it the same way (no csproj -> the directory name).
            if ($case.Gen) { & $script:Gen -Resx (Join-Path $dir 'Strings.resx') *> $null }
            if ($case.ContainsKey('Stale') -and $case.Stale) { Add-Content (Join-Path $dir 'Strings.Designer.cs') '// edited by hand' }
            $problems = @(Test-ResxFamily (Join-Path $dir 'Strings.resx') @('vi', 'ja'))
            $failed = $problems.Count -gt 0
            if ($failed) { $reds++ }
            if ($failed -eq $case.ExpectFailure) {
                Write-Host "  ok    $($case.Name)" -ForegroundColor DarkGray
            }
            else {
                $expected = if ($case.ExpectFailure) { 'a failure' } else { 'a pass' }
                Write-Host "  FAIL  $($case.Name) — expected $expected, got: $($problems -join '; ')" -ForegroundColor Red
                $failures++
            }
        }

        if ($failures -gt 0 -or $reds -lt 4) {
            Write-Host "RESX AUDIT SELF-TEST FAILED: $failures wrong verdict(s), $reds red case(s) (at least 4 required)" -ForegroundColor Red
            exit 1
        }

        Write-Host "resx-audit self-test: $($cases.Count) cases, $reds red, both directions" -ForegroundColor Green
        exit 0
    }
    finally {
        Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
    }
}

$families = @(Find-Families $Root)
if ($families.Count -eq 0) {
    Write-Host 'RESX AUDIT FAILED: no .resx family under src/ — the gate would be vacuous (09_I18N names three languages; the first family arrived with P3 PR-2)' -ForegroundColor Red
    exit 1
}

$all = New-Object System.Collections.Generic.List[string]
foreach ($f in $families) {
    $problems = @(Test-ResxFamily $f.FullName $Cultures)
    foreach ($p in $problems) { $all.Add($p) }
}

if ($all.Count -gt 0) {
    Write-Host "RESX AUDIT FAILED: $($all.Count) problem(s)" -ForegroundColor Red
    foreach ($p in $all) { Write-Host "  $p" -ForegroundColor Red }
    exit 1
}

Write-Host "resx-audit: ok — $($families.Count) family(ies), cultures $($Cultures -join ', '), accessors current" -ForegroundColor Green
exit 0
