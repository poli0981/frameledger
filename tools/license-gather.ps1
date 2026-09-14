#Requires -Version 7.0
<#
.SYNOPSIS
    Writes the licence text of every NuGet package the shipped App and Agent carry into
    legal/licenses/nuget/, or checks that the committed copies are exactly what it would write.

.DESCRIPTION
    legal/THIRD_PARTY_NOTICES.md: "Full license texts must be shipped in legal/licenses/ in release
    packages. Populating that directory is a P4 task, driven by a license-gathering script." This is
    that script (P4 PR-6).

    WHAT SHIPS IS READ, NOT LISTED. The input is the restore output of the two publish roots
    (src/FrameLedger.App and src/FrameLedger.Agent, obj/project.assets.json, the win-x64 target):
    every package that contributes a runtime or native asset and is not suppressed as a private
    build-time dependency (the analyzers, CsWin32). A package that arrives transitively -- the three
    MPL-2.0 helpers under LibreHardwareMonitorLib, the OpenTK and SkiaSharp families under ScottPlot --
    is in the output because it is in the build, whether or not anybody wrote a row for it. When this
    script was written, 77 packages shipped and legal/THIRD_PARTY_NOTICES.md named about fifteen.

    WHERE EACH TEXT COMES FROM, in order, and the file says which:
      1. A licence file inside the package: the nuspec's <license type="file">, or, for a package that
         declares an expression, a LICENSE / COPYING file at its root or under Notices/. Verbatim.
      2. A reviewed override, tools/license-overrides/<id>.txt, for a package that declares only a
         <licenseUrl>: the text behind that URL, fetched once by a person, with its provenance at the top.
         A URL-only package with no override is RED -- this script never fetches anything.
      3. An SPDX expression with no file: MIT's standard text under the package's own <copyright> field
         ("not stated" when the package states none -- this script never writes a copyright line), or,
         for Apache-2.0 and MPL-2.0, the statement that the full text is the shared copy beside it
         (legal/licenses/apache-2.0.txt, mpl-2.0.txt) plus, for MPL-2.0, where the Source Code Form is.
         Any other expression is RED until someone adds its text or an override.
    Third-party notice files a package carries (THIRD-PARTY-NOTICES.TXT, ThirdPartyNotices.txt,
    NOTICES.txt) are copied too, once per distinct content: the Microsoft.Extensions packages carry
    the same 78 KB file twenty-seven times.

    MPL-2.0 EXHIBIT B. A package declaring MPL-2.0 that APPLIES the notice -- a file other than the
    MPL-2.0 text itself containing "Incompatible With Secondary Licenses" -- is RED:
    legal/THIRD_PARTY_NOTICES.md's GPL-compatibility route depends on its absence. The licence text is
    excluded on purpose: MPL-2.0 carries Exhibit B as a template, so a grep that counted it would find
    the sentence in every MPL-2.0 project ever published (docs/spike-notes.md §M4 learned that first).
    A package ships no source, so this reads what the package does ship and each text says how far that
    goes; ruling a package clear is a search of its upstream source, recorded in THIRD_PARTY_NOTICES.

    -Check regenerates into a temporary directory and compares both directions, byte for byte: a
    package that ships with no text, a text for a package that no longer ships, and a hand edit are
    all red. tools/license-check.ps1 runs it on every gate.

    NOT HERE: the .NET runtime a self-contained publish carries. Its version follows the SDK of the
    machine that publishes (a local publish on 2026-09-14 took 10.0.12 while the cache held 10.0.11),
    so its LICENSE and THIRD-PARTY-NOTICES are copied by release.yml from the runtime packs that
    publish actually used, and nothing committed here could be right for every machine.

.PARAMETER Check
    Compare instead of write. Exit 1 on any difference.

.PARAMETER SelfTest
    Run the fixture cases (a fake packages folder and a fake restore output) and exit 0 only if every
    case behaves, including the ones that must be red. build.ps1 runs this before the live check.

.PARAMETER AssetsFiles
    The restore outputs to read. Default: the two publish roots'.

.PARAMETER PackagesRoot
    The NuGet global packages folder. Default: $env:NUGET_PACKAGES, else ~/.nuget/packages.

.PARAMETER OutDir
    Where the texts go. Default: legal/licenses/nuget.
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$SelfTest,
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string[]]$AssetsFiles,
    [string]$PackagesRoot,
    [string]$OutDir,
    [string]$OverridesDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:MitText = @'
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'@

$script:LicenceFileName = '^(licen[cs]e|copying)(\.(txt|md))?$'
$script:NoticeFileName = '^(third[-_ ]?party[-_ ]?notices|notices)(\.(txt|md))?$'
$script:ExhibitB = 'Incompatible With Secondary Licenses'

# CULTURE-INVARIANT matching for file names and licence ids. PowerShell's -match ignores case in the CURRENT culture,
# and under tr-TR "LICENSE" lower-cases to "lıcense" (dotless i): measured 2026-09-15, -Check went red on a machine
# whose only difference was its culture, because every LICENSE.TXT stopped matching and fell back to a template.
function Test-Invariant([string]$Text, [string]$Pattern) {
    return [regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]'IgnoreCase, CultureInvariant')
}

function Get-DefaultPackagesRoot {
    if ($env:NUGET_PACKAGES) { return $env:NUGET_PACKAGES }
    return (Join-Path $HOME '.nuget/packages')
}

function ConvertTo-Crlf([string]$Text) {
    return (($Text -replace "`r`n", "`n") -replace "`n", "`r`n")
}

function Get-ShippedPackages([string[]]$Assets) {
    $found = [ordered]@{}
    foreach ($file in $Assets) {
        if (-not (Test-Path $file)) {
            throw "no restore output at $file -- restore the solution (build.ps1 does) before gathering licences"
        }
        $projectDir = Split-Path (Split-Path $file -Parent) -Parent
        $root = (Split-Path $projectDir -Leaf) -replace '^FrameLedger\.', ''
        $a = Get-Content $file -Raw | ConvertFrom-Json -AsHashtable

        $targetKeys = @($a['targets'].Keys | Where-Object { $_ -like '*/win-x64' })
        if ($targetKeys.Count -ne 1) {
            throw "$file has $($targetKeys.Count) win-x64 targets (expected exactly one): $($a['targets'].Keys -join ', ')"
        }

        $suppressed = @{}
        foreach ($framework in $a['project']['frameworks'].Values) {
            if (-not $framework.ContainsKey('dependencies')) { continue }
            foreach ($dependency in $framework['dependencies'].GetEnumerator()) {
                if (Test-Invariant "$($dependency.Value['suppressParent'])" 'All') {
                    $suppressed[$dependency.Key.ToLowerInvariant()] = $true
                }
            }
        }

        foreach ($entry in $a['targets'][$targetKeys[0]].GetEnumerator()) {
            $t = $entry.Value
            if ($t['type'] -ne 'package') { continue }
            $id, $version = $entry.Key -split '/', 2
            if ($suppressed.ContainsKey($id.ToLowerInvariant())) { continue }

            $assets = @()
            foreach ($section in 'runtime', 'native') {
                if ($t.ContainsKey($section)) {
                    $assets += @($t[$section].Keys | Where-Object { $_ -notlike '*/_._' })
                }
            }
            if ($t.ContainsKey('runtimeTargets')) {
                $assets += @($t['runtimeTargets'].GetEnumerator() |
                        Where-Object { "$($_.Value['rid'])" -like 'win*' } | ForEach-Object { $_.Key })
            }
            if ($assets.Count -eq 0) { continue }

            $key = "$id/$version"
            if (-not $found.Contains($key)) {
                $found[$key] = [pscustomobject]@{
                    Id      = $id
                    Version = $version
                    Roots   = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
                }
            }
            [void]$found[$key].Roots.Add($root)
        }
    }
    # ORDINAL, not Sort-Object: the index and the "first found in" line of a shared notices file follow this order,
    # and a culture-sensitive sort could order two ids differently on a machine with another UI culture than CI's,
    # which -Check would report as drift nobody made.
    $list = [System.Collections.Generic.List[object]]::new()
    foreach ($v in $found.Values) { $list.Add($v) }
    $list.Sort([System.Comparison[object]] { param($x, $y) [string]::CompareOrdinal($x.Id.ToLowerInvariant(), $y.Id.ToLowerInvariant()) })
    return @($list)
}

function Get-OrdinalSorted([string[]]$Items) {
    $array = [string[]]@($Items)
    [System.Array]::Sort($array, [System.StringComparer]::Ordinal)
    # The comma keeps a one-element (or empty) array an array: PowerShell unrolls a returned array otherwise, and
    # LicenceFiles[0] of a lone "LICENSE.txt" would then be "L".
    return , $array
}

function Get-MetadataValue([System.Xml.XmlNode]$Metadata, [string]$Name) {
    $node = $Metadata.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return $null }
    $text = $node.InnerText.Trim()
    if ($text.Length -eq 0) { return $null }
    return $text
}

function Read-Package([string]$Root, [string]$Id, [string]$Version) {
    $dir = Join-Path (Join-Path $Root $Id.ToLowerInvariant()) $Version.ToLowerInvariant()
    $nuspec = Join-Path $dir ($Id.ToLowerInvariant() + '.nuspec')
    if (-not (Test-Path $nuspec)) {
        throw "$Id $Version is in the restore output but not in the packages folder ($nuspec) -- restore first"
    }
    [xml]$x = Get-Content $nuspec -Raw
    $metadata = $x.DocumentElement.SelectSingleNode("*[local-name()='metadata']")
    $licenceNode = $metadata.SelectSingleNode("*[local-name()='license']")
    $files = @(Get-ChildItem $dir -Recurse -File | Where-Object {
            $_.Extension -notin '.dll', '.exe', '.so', '.dylib', '.a', '.lib', '.pdb', '.nupkg', '.p7s', '.png', '.xml' -and
            $_.Name -notlike '*.sha512'
        })
    $relative = { param($f) [System.IO.Path]::GetRelativePath($dir, $f.FullName) -replace '\\', '/' }
    return [pscustomobject]@{
        Id          = $Id
        Version     = $Version
        Dir         = $dir
        LicenceType = if ($licenceNode) { $licenceNode.GetAttribute('type') } else { $null }
        Licence     = if ($licenceNode) { $licenceNode.InnerText.Trim() } else { $null }
        LicenceUrl  = Get-MetadataValue $metadata 'licenseUrl'
        Copyright   = Get-MetadataValue $metadata 'copyright'
        Authors     = Get-MetadataValue $metadata 'authors'
        ProjectUrl  = Get-MetadataValue $metadata 'projectUrl'
        LicenceFiles = Get-OrdinalSorted @($files | Where-Object {
                (Test-Invariant $_.Name $script:LicenceFileName) -and ((& $relative $_) -notmatch '/' -or (Test-Invariant (& $relative $_) '^notices/'))
            } | ForEach-Object { & $relative $_ })
        NoticeFiles = Get-OrdinalSorted @($files | Where-Object {
                (Test-Invariant $_.Name $script:NoticeFileName) -and ((& $relative $_) -notmatch '/' -or (Test-Invariant (& $relative $_) '^notices/'))
            } | ForEach-Object { & $relative $_ })
        TextFiles   = @($files | Where-Object { $_.Extension -in '.txt', '.md', '.nuspec' } | ForEach-Object { $_.FullName })
    }
}

function Read-Text([string]$Path) {
    return ([System.IO.File]::ReadAllText($Path) -replace "`r`n", "`n").TrimEnd()
}

# One package -> @{ Name = file name; Text = content; Notices = @(@{ Name; Text }) }. Throws on anything
# a person has to decide (an unknown expression, a URL with no override, Exhibit B).
function Build-PackageText($Package, [string]$Roots, [string]$Overrides) {
    $p = $Package
    $notices = [System.Collections.Generic.List[object]]::new()
    foreach ($n in $p.NoticeFiles) {
        $text = Read-Text (Join-Path $p.Dir $n)
        $hash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($text))).Substring(0, 12).ToLowerInvariant()
        $notices.Add([pscustomobject]@{ Name = "notices-$hash.txt"; Text = $text; Source = "$($p.Id) $($p.Version) $n" })
    }

    $declared = $null
    $source = $null
    $body = $null

    if ($p.LicenceType -eq 'file') {
        $declared = "the file $($p.Licence) in the package"
        $path = Join-Path $p.Dir $p.Licence
        if (-not (Test-Path $path)) { throw "$($p.Id) $($p.Version) declares licence file '$($p.Licence)' and the package does not contain it" }
        $source = "$($p.Licence), verbatim from the package"
        $body = Read-Text $path
    }
    elseif ($p.LicenceType -eq 'expression') {
        $declared = $p.Licence
        if ($p.LicenceFiles.Count -gt 0) {
            $source = "$($p.LicenceFiles[0]), verbatim from the package"
            $body = Read-Text (Join-Path $p.Dir $p.LicenceFiles[0])
        }
        else {
            switch ($p.Licence) {
                'MIT' {
                    $source = 'the standard MIT text; the package ships no licence file'
                    $holder = if ($p.Copyright) { $p.Copyright } else { "not stated by the package (authors: $($p.Authors))" }
                    $body = "MIT License`n`nCopyright: $holder`n`n" + ($script:MitText -replace "`r`n", "`n").TrimEnd()
                }
                'Apache-2.0' {
                    $source = 'the shared Apache-2.0 text; the package ships no licence file'
                    $body = "Licensed under the Apache License, Version 2.0. The full text is apache-2.0.txt in the directory above this one.`n`nCopyright: " +
                    $(if ($p.Copyright) { $p.Copyright } else { "not stated by the package (authors: $($p.Authors))" })
                }
                'MPL-2.0' {
                    $source = 'the shared MPL-2.0 text; the package ships no licence file'
                    $body = "Subject to the terms of the Mozilla Public License, Version 2.0. The full text is mpl-2.0.txt in the directory above this one.`n`n" +
                    "Source Code Form (MPL-2.0 section 3.2): $(if ($p.ProjectUrl) { $p.ProjectUrl } else { 'not stated by the package' })`n`nCopyright: " +
                    $(if ($p.Copyright) { $p.Copyright } else { "not stated by the package (authors: $($p.Authors))" })
                }
                default {
                    throw "$($p.Id) $($p.Version) declares '$($p.Licence)' and ships no licence file; this script has no text for that expression -- add tools/license-overrides/$($p.Id).txt"
                }
            }
        }
    }
    elseif ($p.LicenceUrl) {
        $declared = "a URL only: $($p.LicenceUrl)"
        $override = Join-Path $Overrides "$($p.Id).txt"
        if (-not (Test-Path $override)) {
            throw "$($p.Id) $($p.Version) declares only a licence URL ($($p.LicenceUrl)); nothing here fetches it -- a person reads it and commits the text as tools/license-overrides/$($p.Id).txt"
        }
        $source = "tools/license-overrides/$($p.Id).txt, a reviewed copy of the text behind that URL"
        $body = Read-Text $override
    }
    else {
        throw "$($p.Id) $($p.Version) declares no licence at all -- it cannot ship until a person establishes one"
    }

    $exhibitChecked = 'not checkable: the package ships no text file; the upstream repository is where it would have to be ruled out'
    if (Test-Invariant $declared 'MPL') {
        $hits = @($p.TextFiles | Where-Object {
                (Select-String -Path $_ -CaseSensitive -SimpleMatch $script:ExhibitB -Quiet) -and
                -not ((Select-String -Path $_ -CaseSensitive -SimpleMatch 'Mozilla Public License Version 2.0' -Quiet) -and
                    (Select-String -Path $_ -CaseSensitive -SimpleMatch 'Exhibit A - Source Code Form License Notice' -Quiet))
            })
        if ($hits.Count -gt 0) {
            throw "$($p.Id) $($p.Version) is MPL-2.0 and carries Exhibit B ('$($script:ExhibitB)') in $($hits -join ', ') -- the GPL-compatibility route in legal/THIRD_PARTY_NOTICES.md does not hold for it"
        }
        if ($p.TextFiles.Count -gt 0) {
            $exhibitChecked = "not found in the package's $($p.TextFiles.Count) text file(s); the package ships no source, so the upstream repository is where it would have to be ruled out"
        }
    }

    $lines = @(
        "$($p.Id) $($p.Version)",
        '',
        "Licence declared: $declared",
        "Text below: $source",
        "Copyright: $(if ($p.Copyright) { $p.Copyright } else { 'not stated by the package' })",
        "Authors: $(if ($p.Authors) { $p.Authors } else { 'not stated' })",
        "Project: $(if ($p.ProjectUrl) { $p.ProjectUrl } else { 'not stated' })",
        "Ships in: $Roots"
    )
    if (Test-Invariant $declared 'MPL') { $lines += "MPL-2.0 Exhibit B: $exhibitChecked" }
    foreach ($n in $notices) { $lines += "Third-party notices: $($n.Name) ($($n.Source.Substring($p.Id.Length + $p.Version.Length + 2)))" }
    $lines += @('', '-------------------------------------------------------------------------------', '', $body)

    return [pscustomobject]@{
        Name     = "$($p.Id).txt"
        Text     = ($lines -join "`n")
        Declared = $declared
        Exhibit  = $exhibitChecked
        Notices  = $notices
    }
}

function Write-Gathered([object[]]$Shipped, [string]$Root, [string]$Overrides, [string]$Destination) {
    New-Item -ItemType Directory -Force $Destination | Out-Null
    $index = [System.Collections.Generic.List[string]]::new()
    $index.Add('# Packages the App and the Agent ship')
    $index.Add('')
    $index.Add('Generated by `tools/license-gather.ps1` from the restore output of the two publish roots. Do not edit a')
    $index.Add('file here by hand: `tools/license-check.ps1` fails when this directory differs from what the script writes.')
    $index.Add('The .NET runtime a self-contained package carries is not listed; `release.yml` copies its licence and notices')
    $index.Add('from the runtime packs the publish used.')
    $index.Add('')
    $index.Add('| Package | Version | Licence | Ships in | Text |')
    $index.Add('|---|---|---|---|---|')
    $written = @{}
    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($s in $Shipped) {
        try {
            $package = Read-Package $Root $s.Id $s.Version
            $built = Build-PackageText $package ($s.Roots -join ', ') $Overrides
        }
        catch {
            $problems.Add($_.Exception.Message)
            continue
        }
        [System.IO.File]::WriteAllText((Join-Path $Destination $built.Name), (ConvertTo-Crlf ($built.Text + "`n")), [System.Text.UTF8Encoding]::new($false))
        foreach ($n in $built.Notices) {
            if (-not $written.ContainsKey($n.Name)) {
                [System.IO.File]::WriteAllText((Join-Path $Destination $n.Name), (ConvertTo-Crlf ("Third-party notices first found in $($n.Source)`n`n" + $n.Text + "`n")), [System.Text.UTF8Encoding]::new($false))
                $written[$n.Name] = $true
            }
        }
        $licence = $built.Declared -replace '\|', '/'
        $index.Add("| $($s.Id) | $($s.Version) | $licence | $($s.Roots -join ', ') | [$($built.Name)]($($built.Name)) |")
    }
    [System.IO.File]::WriteAllText((Join-Path $Destination 'INDEX.md'), (ConvertTo-Crlf (($index -join "`n") + "`n")), [System.Text.UTF8Encoding]::new($false))
    return $problems
}

function Compare-Directories([string]$Expected, [string]$Actual) {
    $differences = [System.Collections.Generic.List[string]]::new()
    $want = @{}
    Get-ChildItem $Expected -File | ForEach-Object { $want[$_.Name.ToLowerInvariant()] = $_ }
    $have = @{}
    if (Test-Path $Actual) { Get-ChildItem $Actual -File | ForEach-Object { $have[$_.Name.ToLowerInvariant()] = $_ } }
    foreach ($k in ($want.Keys | Sort-Object)) {
        if (-not $have.ContainsKey($k)) { $differences.Add("missing: $($want[$k].Name)"); continue }
        $a = [System.IO.File]::ReadAllBytes($want[$k].FullName)
        $b = [System.IO.File]::ReadAllBytes($have[$k].FullName)
        if (-not [System.Linq.Enumerable]::SequenceEqual($a, $b)) { $differences.Add("differs: $($want[$k].Name)") }
    }
    foreach ($k in ($have.Keys | Sort-Object)) {
        if (-not $want.ContainsKey($k)) { $differences.Add("not written by the script (a package that no longer ships, or a hand-added file): $($have[$k].Name)") }
    }
    return $differences
}

function Invoke-Gather([string[]]$Assets, [string]$Root, [string]$Overrides, [string]$Destination, [bool]$CheckOnly) {
    $shipped = Get-ShippedPackages $Assets
    if ($shipped.Count -eq 0) { throw 'the restore output lists no shipped package -- an empty list is a broken input, not a clean tree' }
    if (-not $CheckOnly) {
        if (Test-Path $Destination) { Get-ChildItem $Destination -File | Remove-Item -Force }
        $problems = @(Write-Gathered $shipped $Root $Overrides $Destination)
        return [pscustomobject]@{ Shipped = $shipped.Count; Problems = $problems; Differences = @() }
    }
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("fl-licences-" + [guid]::NewGuid().ToString('N'))
    try {
        $problems = @(Write-Gathered $shipped $Root $Overrides $temp)
        $differences = @(Compare-Directories $temp $Destination)
        return [pscustomobject]@{ Shipped = $shipped.Count; Problems = $problems; Differences = $differences }
    }
    finally {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------------------------ self-test
function Invoke-SelfTest {
    $base = Join-Path ([System.IO.Path]::GetTempPath()) ("fl-licence-selftest-" + [guid]::NewGuid().ToString('N'))
    $failed = 0
    try {
        $packages = Join-Path $base 'packages'
        $overrides = Join-Path $base 'overrides'
        New-Item -ItemType Directory -Force $overrides | Out-Null

        function New-FakePackage([string]$Id, [string]$Version, [string]$LicenceXml, [hashtable]$Files = @{}, [string]$Copyright) {
            $dir = Join-Path (Join-Path $packages $Id.ToLowerInvariant()) $Version
            New-Item -ItemType Directory -Force $dir | Out-Null
            $cr = if ($Copyright) { "<copyright>$Copyright</copyright>" } else { '' }
            $nuspec = "<?xml version=`"1.0`"?><package xmlns=`"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd`"><metadata><id>$Id</id><version>$Version</version><authors>Fake Authors</authors>$LicenceXml$cr<projectUrl>https://example.invalid/$Id</projectUrl></metadata></package>"
            [System.IO.File]::WriteAllText((Join-Path $dir ($Id.ToLowerInvariant() + '.nuspec')), $nuspec)
            foreach ($f in $Files.GetEnumerator()) {
                $path = Join-Path $dir $f.Key
                New-Item -ItemType Directory -Force (Split-Path $path -Parent) | Out-Null
                [System.IO.File]::WriteAllText($path, $f.Value)
            }
        }

        New-FakePackage 'Fake.Mit' '1.0.0' '<license type="expression">MIT</license>' @{} 'Copyright (c) Fake Mit Ltd'
        New-FakePackage 'Fake.MitNoHolder' '1.0.0' '<license type="expression">MIT</license>'
        New-FakePackage 'Fake.File' '2.0.0' '<license type="file">LICENSE.txt</license>' @{ 'LICENSE.txt' = 'FAKE FILE LICENCE'; 'THIRD-PARTY-NOTICES.TXT' = 'NOTICE A' }
        New-FakePackage 'Fake.Apache' '3.0.0' '<license type="expression">Apache-2.0</license>' @{ 'THIRD-PARTY-NOTICES.TXT' = 'NOTICE A' }
        New-FakePackage 'Fake.Analyzer' '1.0.0' '<license type="expression">MIT</license>'
        New-FakePackage 'Fake.Placeholder' '1.0.0' '<license type="expression">MIT</license>'
        New-FakePackage 'Fake.Url' '1.0.0' '<licenseUrl>https://example.invalid/licence</licenseUrl>'
        New-FakePackage 'Fake.MplB' '1.0.0' '<license type="expression">MPL-2.0</license>' @{ 'readme.txt' = 'This Source Code Form is "Incompatible With Secondary Licenses", as defined by the MPL v2.0.' }
        New-FakePackage 'Fake.Upper' '1.0.0' '<license type="expression">MIT</license>' @{ 'LICENSE.TXT' = 'UPPER-CASE LICENCE FILE' }
        New-FakePackage 'Fake.MplText' '1.0.0' '<license type="expression">MPL-2.0</license>' @{ 'LICENSE.txt' = "Mozilla Public License Version 2.0`n...`nExhibit A - Source Code Form License Notice`n...`nExhibit B - `"Incompatible With Secondary Licenses`" Notice`n  This Source Code Form is `"Incompatible With Secondary Licenses`", as defined by the Mozilla Public License, v. 2.0." }

        function New-Assets([string]$Project, [string[]]$Ids) {
            $dir = Join-Path (Join-Path $base "src/$Project") 'obj'
            New-Item -ItemType Directory -Force $dir | Out-Null
            $versions = @{ 'Fake.Mit' = '1.0.0'; 'Fake.MitNoHolder' = '1.0.0'; 'Fake.File' = '2.0.0'; 'Fake.Apache' = '3.0.0'; 'Fake.Analyzer' = '1.0.0'; 'Fake.Placeholder' = '1.0.0'; 'Fake.Url' = '1.0.0'; 'Fake.MplB' = '1.0.0'; 'Fake.MplText' = '1.0.0'; 'Fake.Upper' = '1.0.0' }
            $target = [ordered]@{}
            foreach ($id in $Ids) {
                $runtime = if ($id -eq 'Fake.Placeholder') { @{ 'lib/net10.0/_._' = @{} } } else { @{ "lib/net10.0/$id.dll" = @{} } }
                $target["$id/$($versions[$id])"] = @{ type = 'package'; runtime = $runtime }
            }
            $assets = @{
                version  = 3
                targets  = @{ 'net10.0-windows10.0.22621.0' = @{}; 'net10.0-windows10.0.22621.0/win-x64' = $target }
                project  = @{ frameworks = @{ 'net10.0-windows10.0.22621.0' = @{ dependencies = @{ 'Fake.Analyzer' = @{ suppressParent = 'All' } } } } }
            }
            $path = Join-Path $dir 'project.assets.json'
            [System.IO.File]::WriteAllText($path, ($assets | ConvertTo-Json -Depth 10))
            return $path
        }

        $good = @(
            (New-Assets 'FrameLedger.App' @('Fake.Mit', 'Fake.File', 'Fake.Analyzer', 'Fake.Placeholder')),
            (New-Assets 'FrameLedger.Agent' @('Fake.Mit', 'Fake.MitNoHolder', 'Fake.Apache'))
        )
        $out = Join-Path $base 'out'

        $case = {
            param([string]$Name, [scriptblock]$Body)
            try {
                $ok = & $Body
            }
            catch {
                $ok = $false
                Write-Host "       ($($_.Exception.Message))" -ForegroundColor DarkGray
            }
            Write-Host ("  {0,-4} {1}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $Name) -ForegroundColor ($(if ($ok) { 'DarkGray' } else { 'Red' }))
            if (-not $ok) { $script:selfTestFailed++ }
        }
        $script:selfTestFailed = 0

        & $case 'the shipped set is read from the restore output: suppressed and placeholder-only packages are not in it' {
            $r = Invoke-Gather $good $packages $overrides $out $false
            $names = @(Get-ChildItem $out -File | ForEach-Object Name | Sort-Object)
            $r.Problems.Count -eq 0 -and $r.Shipped -eq 4 -and
            ($names -join ',') -eq 'Fake.Apache.txt,Fake.File.txt,Fake.Mit.txt,Fake.MitNoHolder.txt,INDEX.md,notices-' + ([System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes('NOTICE A'))).Substring(0, 12).ToLowerInvariant()) + '.txt'
        }
        & $case 'a package in both roots says so once' {
            (Get-Content (Join-Path $out 'Fake.Mit.txt') -Raw) -match 'Ships in: Agent, App'
        }
        & $case "MIT under the package's own copyright line, verbatim" {
            $t = Get-Content (Join-Path $out 'Fake.Mit.txt') -Raw
            $t -match 'Copyright: Copyright \(c\) Fake Mit Ltd' -and $t -match 'Permission is hereby granted, free of charge'
        }
        & $case 'no copyright line is invented when the package states none' {
            (Get-Content (Join-Path $out 'Fake.MitNoHolder.txt') -Raw) -match 'Copyright: not stated by the package \(authors: Fake Authors\)'
        }
        & $case 'a licence file is copied verbatim and identical notices are written once' {
            (Get-Content (Join-Path $out 'Fake.File.txt') -Raw) -match 'FAKE FILE LICENCE' -and
            @(Get-ChildItem $out -Filter 'notices-*.txt').Count -eq 1
        }
        & $case '-Check on what was just written is green' {
            $r = Invoke-Gather $good $packages $overrides $out $true
            $r.Problems.Count -eq 0 -and $r.Differences.Count -eq 0
        }
        & $case '-Check after a hand edit is RED' {
            Add-Content (Join-Path $out 'Fake.Mit.txt') 'edited by hand'
            $r = Invoke-Gather $good $packages $overrides $out $true
            @($r.Differences | Where-Object { $_ -like 'differs: Fake.Mit.txt' }).Count -eq 1
        }
        & $case '-Check with a text for a package that no longer ships is RED' {
            [void](Invoke-Gather $good $packages $overrides $out $false)
            Set-Content (Join-Path $out 'Gone.Package.txt') 'stale'
            $r = Invoke-Gather $good $packages $overrides $out $true
            @($r.Differences | Where-Object { $_ -like '*Gone.Package.txt' }).Count -eq 1
        }
        & $case 'the output does not depend on the machine culture (tr-TR lower-cases LICENSE to lıcense)' {
            $assets = @(New-Assets 'FrameLedger.Culture' @('Fake.Upper', 'Fake.Apache'))
            $turkish = Join-Path $base 'out-tr'
            $invariant = Join-Path $base 'out-invariant'
            # A CHILD PROCESS, on purpose. .NET fixes a case-insensitive Regex's casing when the Regex is built and
            # PowerShell caches Regex objects per pattern, so switching the culture inside this process after the
            # cases above tests nothing: measured 2026-09-15, a culture-sensitive mutant passed that way.
            $command = "[System.Globalization.CultureInfo]::CurrentCulture = 'tr-TR'; & '$PSCommandPath' -AssetsFiles '$($assets[0])' -PackagesRoot '$packages' -OverridesDir '$overrides' -OutDir '$turkish' *> `$null; exit `$LASTEXITCODE"
            & pwsh -NoProfile -NonInteractive -Command $command
            if ($LASTEXITCODE -ne 0) { return $false }
            [void](Invoke-Gather $assets $packages $overrides $invariant $false)
            @(Compare-Directories $invariant $turkish).Count -eq 0 -and
            (Get-Content (Join-Path $turkish 'Fake.Upper.txt') -Raw) -match 'UPPER-CASE LICENCE FILE'
        }
        & $case 'a package that declares only a URL, with no reviewed override, is RED' {
            $assets = @(New-Assets 'FrameLedger.App' @('Fake.Url'))
            $r = Invoke-Gather $assets $packages $overrides (Join-Path $base 'out-url') $false
            @($r.Problems | Where-Object { $_ -like '*Fake.Url*licence URL*' }).Count -eq 1
        }
        & $case 'the same package with a reviewed override is green and names the override' {
            Set-Content (Join-Path $overrides 'Fake.Url.txt') 'REVIEWED URL TEXT'
            $assets = @(New-Assets 'FrameLedger.App' @('Fake.Url'))
            $r = Invoke-Gather $assets $packages $overrides (Join-Path $base 'out-url') $false
            $r.Problems.Count -eq 0 -and (Get-Content (Join-Path $base 'out-url/Fake.Url.txt') -Raw) -match 'REVIEWED URL TEXT'
        }
        & $case 'an MPL-2.0 package carrying Exhibit B is RED' {
            $assets = @(New-Assets 'FrameLedger.App' @('Fake.MplB'))
            $r = Invoke-Gather $assets $packages $overrides (Join-Path $base 'out-mpl') $false
            @($r.Problems | Where-Object { $_ -like '*Fake.MplB*Exhibit B*' }).Count -eq 1
        }
        & $case 'the MPL-2.0 text itself, which carries Exhibit B as a template, is NOT an applied notice' {
            $assets = @(New-Assets 'FrameLedger.App' @('Fake.MplText'))
            $r = Invoke-Gather $assets $packages $overrides (Join-Path $base 'out-mpltext') $false
            $r.Problems.Count -eq 0
        }
        & $case 'a missing restore output is RED, not an empty list' {
            try {
                [void](Invoke-Gather @((Join-Path $base 'nowhere/project.assets.json')) $packages $overrides $out $true)
                $false
            }
            catch { $_.Exception.Message -like '*no restore output*' }
        }
        $failed = $script:selfTestFailed
    }
    finally {
        Remove-Item $base -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($failed -gt 0) {
        Write-Host "license-gather self-test FAILED ($failed case(s))" -ForegroundColor Red
        return 1
    }
    Write-Host 'license-gather self-test OK' -ForegroundColor Green
    return 0
}

# ------------------------------------------------------------------------------------------------ main
if ($SelfTest) { exit (Invoke-SelfTest) }

if (-not $AssetsFiles) {
    $AssetsFiles = @(
        (Join-Path $RepoRoot 'src/FrameLedger.App/obj/project.assets.json'),
        (Join-Path $RepoRoot 'src/FrameLedger.Agent/obj/project.assets.json')
    )
}
if (-not $PackagesRoot) { $PackagesRoot = Get-DefaultPackagesRoot }
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'legal/licenses/nuget' }
if (-not $OverridesDir) { $OverridesDir = Join-Path $RepoRoot 'tools/license-overrides' }

$result = Invoke-Gather $AssetsFiles $PackagesRoot $OverridesDir $OutDir ([bool]$Check)
foreach ($p in $result.Problems) { Write-Host "  - $p" -ForegroundColor Red }
foreach ($d in $result.Differences) { Write-Host "  - $d" -ForegroundColor Red }
if ($result.Problems.Count -gt 0 -or $result.Differences.Count -gt 0) {
    if ($Check) {
        Write-Host "LICENCE TEXTS OUT OF DATE: legal/licenses/nuget is not what tools/license-gather.ps1 writes for the $($result.Shipped) shipped package(s) -- run it and commit the result" -ForegroundColor Red
    }
    else {
        Write-Host "LICENCE GATHERING INCOMPLETE: $($result.Problems.Count) package(s) need a person's decision" -ForegroundColor Red
    }
    exit 1
}
Write-Host ("licence texts {0} for {1} shipped package(s)" -f $(if ($Check) { 'current' } else { 'written' }), $result.Shipped) -ForegroundColor Green
exit 0
