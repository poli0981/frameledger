# SPDX-License-Identifier: GPL-3.0-only
# Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

#Requires -Version 7.0
<#
.SYNOPSIS
    Every member of a vendor's struct the Overlay reads for Advanced metrics is on a committed list, and a camera's
    position, axes and matrices are read nowhere in the code loaded into a game.

.DESCRIPTION
    CLAUDE.md rule 4 lets the Overlay read the arguments of the calls it hooks. Advanced metrics, train B (owner
    decisions D111-D116, 2026-11-11) reads more of them than the frame record ever needed - and the same structs,
    a few members further on, hold where the player's camera is and which way it looks. "We read the sharpness and
    not the camera" is a sentence a reviewer would be asked to check by reading a 2,700-line file; this checks it.

    WHAT IT HOLDS:

      1. The reads are in ONE header, src/native/FrameLedger.Overlay/include/fl_adv_fields.h, through parameters with
         fixed names - `o` (sl::DLSSOptions), `d` (ffxDispatchDescUpscale), `m` (FfxApiEffectMemoryUsage), `q` (the
         memory query's descriptor), `k` (sl::Constants) and `g` (ffxConfigureDescFrameGeneration), both since
         0.1.0-beta.43, and `desc` (a query's or a configure's header: one type, ffxApiHeader). Every member read through them is on
         tools/overlay-allowed-fields.txt, and every name on that list is read.
      2. Those parameters are not aliased: each of the six letters appears only as `x.member` or where it is
         declared, and `->` is applied to nothing but `desc` and the Overlay's own `region`. So a member cannot be
         read under a name this check does not look at.
      3. No other source of the Overlay or the Vulkan layer reads a member the list names (the names every struct
         has - structVersion, type, flags, reset - apart): the header is the one place.
      4. A camera's position, axes and matrices are named NOWHERE in the Overlay's or the Vulkan layer's code, and
         not on the list either - whatever the list says. The names are Streamline's (sl::Constants) and
         FidelityFX's (the frame-generation prepare descriptor).
      5. Neither of the two structs that hold them is copied whole or measured: no variable of the type, no
         sizeof - a copy of the struct is a copy of the camera.

    Rules 4 and 5 judge the code and the list, so the way to let a camera in is not "add a line to the text file".

    WHAT IT DOES NOT DO. It reads names, not types: it cannot see a member reached by pointer arithmetic or a struct
    copied with memcpy, and no code here does either. It does not know which struct version carries a member -
    docs/17_HOOK_ENGINE.md section "Which struct version carries which field" does, and ctest fl_adv_fields checks
    that a struct of a lower version is not read past its end. It does not look at the tools and tests, which are
    not loaded into a game.

    -SelfTest proves every rule in both directions on in-memory sources.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The header's parameters and the struct each one is. `q` is either version of the memory query: one member, one name.
$script:Parameter = [ordered]@{
    o    = 'sl::DLSSOptions'
    d    = 'ffxDispatchDescUpscale'
    m    = 'FfxApiEffectMemoryUsage'
    q    = 'ffxQueryDescUpscaleGetGPUMemoryUsage'
    k    = 'sl::Constants'
    g    = 'ffxConfigureDescFrameGeneration'
    fv   = 'sl::FeatureVersion'
    fr   = 'sl::FeatureRequirements'
    # Train C: what NVIDIA's driver wrote into the Overlay's own buffers (fl_reflex.h makes the two calls). `lf` is
    # one frame of the report, handed on by index - `lr.frameReport[i]` - and never bound to a name of its own.
    lr   = 'NV_LATENCY_RESULT_PARAMS'
    lf   = 'NV_LATENCY_RESULT_PARAMS::FrameReport'
    ls   = 'NV_GET_SLEEP_STATUS_PARAMS'
    desc = 'ffxQueryDescHeader'
}

# Where a camera is and which way it looks: sl::Constants' members, then the frame-generation prepare descriptor's.
# Exact tokens - cameraNear, cameraFar, cameraFOV, cameraAspectRatio and cameraFovAngleVertical are numbers a settings
# menu shows, and are not these.
$script:Camera = @(
    'cameraPos', 'cameraUp', 'cameraRight', 'cameraFwd', 'cameraViewToClip', 'clipToCameraView', 'clipToLensClip',
    'clipToPrevClip', 'prevClipToClip', 'cameraPinholeOffset',
    'cameraPosition', 'cameraForward'
)

# What identifies the adapter a game asks Streamline about (sl::AdapterInfo, owner decision D131): its LUID, the
# size of it, or a Vulkan physical device. Named nowhere in the code loaded into a game.
$script:Adapter = @('deviceLUID', 'deviceLUIDSizeInBytes', 'vkPhysicalDevice')

# The two structs that hold one.
$script:CameraStruct = @('sl::Constants', 'ffxDispatchDescFrameGenerationPrepareV2')

# Member names every struct has. The header's reads of them are held to the list like any other; what rule 3 cannot do
# is refuse them elsewhere, where they are members of the Overlay's own structs and of descriptors read since 0.1.0.
# `frameID` joined them with train C: the driver's latency report has one, and so has the frame-generation prepare
# descriptor the Overlay has read since 0.1.0 (dllmain.cpp counts application frames by it).
$script:SharedMember = @('structVersion', 'type', 'flags', 'reset', 'frameID')

# A C++ source without its comments and string literals: a camera named in the comment that says it is never read is
# not a read.
function Get-Code([string]$Source) {
    $code = [regex]::Replace($Source, '/\*.*?\*/', ' ', 'Singleline')
    $code = [regex]::Replace($code, '//[^\r\n]*', ' ')
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
        if ($line -cnotmatch '^[A-Za-z_][\w:]*::[A-Za-z_]\w*$') { $problems.Add("the list's line $number is not one Struct::member: '$line'"); continue }
        if ($names.Contains($line)) { $problems.Add("the list names $line twice"); continue }
        $names.Add($line)
    }
    return @{ Names = @($names); Problems = @($problems) }
}

# The members the header reads, as Struct::member, and where a parameter is used in a way rule 2 refuses.
function Get-HeaderRead([string]$Code) {
    $reads = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    $problems = [System.Collections.Generic.List[string]]::new()

    foreach ($m in [regex]::Matches($Code, '(?<![\w.>:])(o|d|m|q|k|g|fv|fr|lr|lf|ls)\b(\s*\.\s*([A-Za-z_]\w*))?')) {
        $name = $m.Groups[1].Value
        if ($m.Groups[3].Success) {
            [void]$reads.Add("$($script:Parameter[$name])::$($m.Groups[3].Value)")
            continue
        }
        # Not a member access: it may only be the parameter's own declaration - `Type& o,` or `const auto& q =`.
        $before = $Code.Substring([Math]::Max(0, $m.Index - 8), [Math]::Min(8, $m.Index))
        $after = $Code.Substring($m.Index + $m.Length, [Math]::Min(4, $Code.Length - $m.Index - $m.Length))
        if ($before -cnotmatch '&\s*$' -or $after -cnotmatch '^\s*[,)=]') {
            $problems.Add("the header uses the parameter '$name' other than as $name.member: an alias is a member read under a name nobody checks")
        }
    }
    foreach ($m in [regex]::Matches($Code, '(?<![\w.>:])([A-Za-z_]\w*)\s*->\s*([A-Za-z_]\w*)')) {
        $name = $m.Groups[1].Value
        if ($name -ceq 'desc') { [void]$reads.Add("$($script:Parameter['desc'])::$($m.Groups[2].Value)") }
        elseif ($name -cne 'region') { $problems.Add("the header reads '$name->$($m.Groups[2].Value)': only 'desc' and the Overlay's own 'region' are reached through a pointer") }
    }
    return @{ Reads = @($reads); Problems = @($problems) }
}

# $Others: file name -> source, for every Overlay and Vulkan-layer source but the header.
function Test-Field([string]$Header, [string]$ListText, [hashtable]$Others) {
    $problems = [System.Collections.Generic.List[string]]::new()
    $list = Read-AllowedList $ListText
    foreach ($p in $list.Problems) { $problems.Add($p) }
    $allowed = $list.Names

    $headerCode = Get-Code $Header
    $read = Get-HeaderRead $headerCode
    foreach ($p in $read.Problems) { $problems.Add($p) }
    if ($read.Reads.Count -eq 0) { $problems.Add('no member read was found in the header: the check read the wrong file, or nothing') }
    foreach ($name in $read.Reads) {
        if ($allowed -cnotcontains $name) { $problems.Add("the header reads $name, which the list does not allow") }
    }
    foreach ($name in $allowed) {
        if ($read.Reads -cnotcontains $name) { $problems.Add("the list allows $name, which the header does not read") }
        $member = $name.Substring($name.LastIndexOf('::') + 2)
        if ($script:Camera -ccontains $member) { $problems.Add("$name is where a camera is or looks: never read (rule 4), whatever the list says") }
    }

    $members = @($allowed | ForEach-Object { $_.Substring($_.LastIndexOf('::') + 2) } | Where-Object { $script:SharedMember -cnotcontains $_ } | Sort-Object -Unique)
    $sources = @{ 'the header' = $headerCode }
    foreach ($key in $Others.Keys) { $sources[$key] = Get-Code $Others[$key] }
    foreach ($key in ($sources.Keys | Sort-Object)) {
        $code = $sources[$key]
        foreach ($token in $script:Camera) {
            if ($code -cmatch "(?<!\w)$token(?!\w)") { $problems.Add("$key names $token - where a camera is or looks is read nowhere in code loaded into a game (rule 4)") }
        }
        foreach ($token in $script:Adapter) {
            if ($code -cmatch "(?<!\w)$token(?!\w)") { $problems.Add("$key names $token - which adapter a game asks Streamline about is read nowhere in code loaded into a game (D131)") }
        }
        foreach ($struct in $script:CameraStruct) {
            $bare = [regex]::Escape($struct)
            if ($code -cmatch "sizeof\s*\(\s*$bare\s*\)") { $problems.Add("$key measures ${struct}: the struct holds a camera, and is neither measured nor copied") }
            if ($code -cmatch "(?<![\w:])$bare\s+[A-Za-z_]\w*\s*[;={\[]") { $problems.Add("$key declares a $struct by value: a copy of the struct is a copy of the camera") }
        }
        if ($key -cne 'the header') {
            foreach ($member in $members) {
                if ($code -cmatch "(\.|->)\s*$member(?!\w)") { $problems.Add("$key reads a member named ${member}: the extra members of a vendor's struct are read in fl_adv_fields.h and nowhere else") }
            }
        }
    }
    return , @($problems)
}

if ($SelfTest) {
    $failed = 0
    $goodHeader = @'
// d.cameraPosition is never read, nor o.cameraPos.
inline void ObserveA(const sl::DLSSOptions& o, Accumulators& a) noexcept {
    a.w.store(o.outputWidth);   /* nor
       d.cameraUp */
    a.v.store(o . structVersion);
}
inline void ObserveB(uint32_t leaf, const ffxDispatchDescUpscale& d, Accumulators& a) noexcept {
    Log("d.cameraRight");
    a.j.store(d.jitterOffset.x + d.jitterOffset.y + d.cameraNear);
}
inline void ObserveC(const FfxApiEffectMemoryUsage& m, Accumulators& a) noexcept { a.t.store(m.totalUsageInBytes); }
inline void ObserveD(const ffxQueryDescHeader* desc, Accumulators& a) noexcept {
    if (desc == nullptr) { return; }
    if (desc->type == 1) {
        const auto& q = *reinterpret_cast<const ffxQueryDescUpscaleGetGPUMemoryUsage*>(desc);
        ObserveC(*q.gpuMemoryUsageUpscaler, a);
    }
}
inline void Publish(Accumulators& a, FlAdvancedState* region) noexcept { region->samples = a.n.load(); }
'@
    $goodList = "# a comment`nsl::DLSSOptions::outputWidth`nsl::DLSSOptions::structVersion`n`nffxDispatchDescUpscale::jitterOffset`nffxDispatchDescUpscale::cameraNear`nFfxApiEffectMemoryUsage::totalUsageInBytes`nffxQueryDescHeader::type`nffxQueryDescUpscaleGetGPUMemoryUsage::gpuMemoryUsageUpscaler`n"
    $goodOther = @{ 'dllmain.cpp' = "void Hook() { if (up->renderSize.width != 0 && desc->type == 3 && c.flags != 0) { fl::adv::ObserveB(leaf, *up, g_adv); } }`n// prep->cameraPosition is never read`nconst auto* prep = reinterpret_cast<const ffxDispatchDescFrameGenerationPrepareV2*>(desc);`nstatic_assert(offsetof(ffxDispatchDescFrameGenerationPrepareV2, renderSize) == 48);" }
    $cases = @(
        @{ Name = 'a header whose reads are the list passes, with a camera named only in comments and strings'; Header = $goodHeader; List = $goodList; Other = $goodOther; Expect = 0 },
        @{ Name = 'a member the list does not allow FAILS'; Header = $goodHeader + "`ninline void X(const ffxDispatchDescUpscale& d) { Keep(d.sharpness); }"; List = $goodList; Other = $goodOther; Expect = 1 },
        @{ Name = 'a name on the list that nothing reads FAILS'; Header = $goodHeader; List = $goodList + "ffxDispatchDescUpscale::sharpness`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'a camera position read in the header FAILS twice: not allowed, and named'; Header = $goodHeader + "`ninline void X(const ffxDispatchDescUpscale& d) { Keep(d.cameraPosition[0]); }"; List = $goodList; Other = $goodOther; Expect = 2 },
        @{ Name = 'a camera position FAILS even when it is listed and read'; Header = $goodHeader + "`ninline void X(const ffxDispatchDescUpscale& d) { Keep(d.cameraPosition[0]); }"; List = $goodList + "ffxDispatchDescUpscale::cameraPosition`n"; Other = $goodOther; Expect = 2 },
        @{ Name = 'a camera matrix named in another Overlay source FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'void Hook(const sl::Constants* c) { Keep(c->cameraViewToClip); }' }; Expect = 1 },
        @{ Name = 'a camera axis named in the Vulkan layer FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'layer.cpp' = 'float x = k.cameraFwd.x;' }; Expect = 1 },
        @{ Name = 'the depth range and the field of view are not a camera position'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'float n = cameraNearOf(c); float f = k_cameraFar; float v = cameraFOV; float r = cameraAspectRatio; float w = cameraFovAngleVertical;' }; Expect = 0 },
        @{ Name = 'an alias of a parameter FAILS'; Header = $goodHeader + "`ninline void X(const sl::DLSSOptions& o) { const auto& x = o; Keep(x.mode); }"; List = $goodList; Other = $goodOther; Expect = 1 },
        @{ Name = 'a parameter passed on whole FAILS'; Header = $goodHeader + "`ninline void X(const ffxDispatchDescUpscale& d) { Other(d); }"; List = $goodList; Other = $goodOther; Expect = 1 },
        @{ Name = 'a member read through another pointer FAILS'; Header = $goodHeader + "`ninline void X(const ffxDispatchDescUpscale* up) { Keep(up->sharpness); }"; List = $goodList; Other = $goodOther; Expect = 1 },
        @{ Name = 'a listed member read outside the header FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'void Hook() { Keep(up->jitterOffset.x); }' }; Expect = 1 },
        @{ Name = 'a member every struct has, read outside the header, passes'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'void Hook() { Keep(s->structVersion + h.type); }' }; Expect = 0 },
        @{ Name = 'sizeof of the struct that holds the camera FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'static_assert(sizeof(sl::Constants) == 444);' }; Expect = 1 },
        @{ Name = 'a copy of the struct that holds the camera FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'void Hook(const void* p) { ffxDispatchDescFrameGenerationPrepareV2 copy = *Prep(p); (void)copy; }' }; Expect = 1 },
        @{ Name = 'a pointer to it, and offsetof into it, pass'; Header = $goodHeader; List = $goodList; Other = $goodOther; Expect = 0 },
        @{ Name = 'a name listed twice FAILS'; Header = $goodHeader; List = $goodList + "sl::DLSSOptions::outputWidth`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'a line that is not one Struct::member FAILS'; Header = $goodHeader; List = $goodList + "sl::DLSSOptions::outputWidth  # the size`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'sl::Constants read through its parameter, each member on the list, passes'; Header = $goodHeader + "`ninline void ObserveK(const sl::Constants& k, uint32_t viewport, Accumulators& a) noexcept { a.n.store(k.cameraNear + k.jitterOffset.x); }"; List = $goodList + "sl::Constants::cameraNear`nsl::Constants::jitterOffset`n"; Other = $goodOther; Expect = 0 },
        @{ Name = 'a member of sl::Constants the list does not allow FAILS'; Header = $goodHeader + "`ninline void ObserveK(const sl::Constants& k, Accumulators& a) noexcept { a.n.store(k.cameraNear + k.cameraFOV); }"; List = $goodList + "sl::Constants::cameraNear`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'the camera read through the sl::Constants parameter FAILS, listed or not'; Header = $goodHeader + "`ninline void ObserveK(const sl::Constants& k, Accumulators& a) noexcept { a.n.store(k.cameraPos.x); }"; List = $goodList + "sl::Constants::cameraPos`n"; Other = $goodOther; Expect = 2 },
        @{ Name = 'the sl::Constants parameter handed on whole FAILS'; Header = $goodHeader + "`ninline void ObserveK(const sl::Constants& k, Accumulators& a) noexcept { a.n.store(k.cameraNear); Other(k); }"; List = $goodList + "sl::Constants::cameraNear`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'the frame-generation configuration read through its parameter, each member on the list, passes'; Header = $goodHeader + "`ninline void ObserveG(const ffxConfigureDescHeader* desc, Accumulators& a) noexcept { const auto& g = *reinterpret_cast<const ffxConfigureDescFrameGeneration*>(desc); a.n.store(g.flags + g.generationRect.left); }"; List = $goodList + "ffxConfigureDescFrameGeneration::flags`nffxConfigureDescFrameGeneration::generationRect`n"; Other = $goodOther; Expect = 0 },
        @{ Name = 'a member of the frame-generation configuration the list does not allow FAILS'; Header = $goodHeader + "`ninline void ObserveG(const ffxConfigureDescHeader* desc, Accumulators& a) noexcept { const auto& g = *reinterpret_cast<const ffxConfigureDescFrameGeneration*>(desc); a.n.store(g.flags); Keep(g.swapChain); }"; List = $goodList + "ffxConfigureDescFrameGeneration::flags`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'the answers about a feature read through their parameters, each member on the list, pass'; Header = $goodHeader + "`ninline void ObserveV(uint32_t feature, const sl::FeatureVersion& fv, Accumulators& a) noexcept { Put(a.f, fv.versionSL); }`ninline void ObserveR(uint32_t feature, const sl::FeatureRequirements& fr, Accumulators& a) noexcept { a.n.store(fr.numRequiredTags); Put(a.f, fr.osVersionDetected); }"; List = $goodList + "sl::FeatureVersion::versionSL`nsl::FeatureRequirements::numRequiredTags`nsl::FeatureRequirements::osVersionDetected`n"; Other = $goodOther; Expect = 0 },
        @{ Name = 'the list of required tags, a pointer of the game''s, FAILS when it is not on the list'; Header = $goodHeader + "`ninline void ObserveR(uint32_t feature, const sl::FeatureRequirements& fr, Accumulators& a) noexcept { a.n.store(fr.numRequiredTags); Keep(fr.requiredTags[0]); }"; List = $goodList + "sl::FeatureRequirements::numRequiredTags`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'a member of the answer read outside the header FAILS'; Header = $goodHeader + "`ninline void ObserveR(uint32_t feature, const sl::FeatureRequirements& fr, Accumulators& a) noexcept { a.n.store(fr.numRequiredTags); }"; List = $goodList + "sl::FeatureRequirements::numRequiredTags`n"; Other = @{ 'dllmain.cpp' = 'void Hook(sl::FeatureRequirements& requirements) { Keep(requirements.numRequiredTags); }' }; Expect = 1 },
        @{ Name = 'the adapter a game asks about, forwarded by its parameter and never looked into, passes'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'sl::Result Hook(sl::Feature feature, const sl::AdapterInfo& adapterInfo) { return g_orig(feature, adapterInfo); } // never adapterInfo.deviceLUID' }; Expect = 0 },
        @{ Name = 'the adapter''s LUID read in the detour FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'dllmain.cpp' = 'sl::Result Hook(sl::Feature feature, const sl::AdapterInfo& adapterInfo) { Keep(adapterInfo.deviceLUID[0]); return g_orig(feature, adapterInfo); }' }; Expect = 1 },
        @{ Name = 'a Vulkan physical device taken from the adapter in the layer FAILS'; Header = $goodHeader; List = $goodList; Other = @{ 'layer.cpp' = 'void* device = info->vkPhysicalDevice;' }; Expect = 1 },
        @{ Name = 'the driver''s latency report read through its parameters, a frame handed on by index, passes'; Header = $goodHeader + "`ninline uint64_t IdOf(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf) noexcept { return lf.frameID + lf.simStartTime; }`ninline void ObserveL(const NV_LATENCY_RESULT_PARAMS& lr, Tally& t) noexcept { t.n += IdOf(lr.frameReport[3]); }`ninline uint32_t ObserveS(const NV_GET_SLEEP_STATUS_PARAMS& ls) noexcept { return ls.bLowLatencyMode; }"; List = $goodList + "NV_LATENCY_RESULT_PARAMS::frameReport`nNV_LATENCY_RESULT_PARAMS::FrameReport::frameID`nNV_LATENCY_RESULT_PARAMS::FrameReport::simStartTime`nNV_GET_SLEEP_STATUS_PARAMS::bLowLatencyMode`n"; Other = $goodOther; Expect = 0 },
        @{ Name = 'a member of a latency frame the list does not allow FAILS'; Header = $goodHeader + "`ninline uint64_t IdOf(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf) noexcept { return lf.frameID + lf.inputSampleTime; }"; List = $goodList + "NV_LATENCY_RESULT_PARAMS::FrameReport::frameID`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'a frame of the report bound to a name of its own FAILS'; Header = $goodHeader + "`ninline void ObserveL(const NV_LATENCY_RESULT_PARAMS& lr) noexcept { const auto& f = lr.frameReport[0]; Keep(f.simStartTime); const auto& whole = lr; Keep(whole); }"; List = $goodList + "NV_LATENCY_RESULT_PARAMS::frameReport`n"; Other = $goodOther; Expect = 1 },
        @{ Name = 'the frame id, which the prepare descriptor has too, is read outside the header; a time of the report is not'; Header = $goodHeader + "`ninline uint64_t IdOf(const NV_LATENCY_RESULT_PARAMS::FrameReport& lf) noexcept { return lf.frameID + lf.simStartTime; }"; List = $goodList + "NV_LATENCY_RESULT_PARAMS::FrameReport::frameID`nNV_LATENCY_RESULT_PARAMS::FrameReport::simStartTime`n"; Other = @{ 'dllmain.cpp' = 'void Hook() { Keep(prep->frameID); Keep(report.frameReport); Keep(r.simStartTime); }' }; Expect = 1 },
        @{ Name = 'a header with no read at all FAILS'; Header = '// o.outputWidth is somewhere else'; List = "# nothing`n"; Other = $goodOther; Expect = 1 }
    )
    foreach ($c in $cases) {
        $problems = Test-Field $c.Header $c.List $c.Other
        $ok = $problems.Count -eq $c.Expect
        Write-Host ("  {0,-4} {1}{2}" -f ($(if ($ok) { 'ok' } else { 'FAIL' })), $c.Name, ($(if (-not $ok) { " -> $($problems.Count): $($problems -join '; ')" } else { '' })))
        if (-not $ok) { $failed++ }
    }
    if ($failed -gt 0) { Write-Host "overlay-fields-check self-test FAILED ($failed)" -ForegroundColor Red; exit 1 }
    Write-Host "overlay-fields-check self-test: $($cases.Count) cases, both directions" -ForegroundColor Green
    exit 0
}

$headerPath = Join-Path $RepoRoot 'src/native/FrameLedger.Overlay/include/fl_adv_fields.h'
$listPath = Join-Path $RepoRoot 'tools/overlay-allowed-fields.txt'
foreach ($path in $headerPath, $listPath) {
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "OVERLAY FIELDS CHECK FAILED: $path is missing" -ForegroundColor Red; exit 1 }
}
$others = @{}
foreach ($dir in 'src/native/FrameLedger.Overlay', 'src/native/FrameLedger.VkLayer') {
    $root = Join-Path $RepoRoot $dir
    if (-not (Test-Path -LiteralPath $root)) { Write-Host "OVERLAY FIELDS CHECK FAILED: $root is missing" -ForegroundColor Red; exit 1 }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Include '*.cpp', '*.h', '*.inl') {
        if ($file.FullName -eq (Resolve-Path -LiteralPath $headerPath).Path) { continue }
        $others[[IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace('\', '/')] = Get-Content -LiteralPath $file.FullName -Raw
    }
}
if ($others.Count -eq 0) { Write-Host 'OVERLAY FIELDS CHECK FAILED: no Overlay or Vulkan-layer source was found beside the header' -ForegroundColor Red; exit 1 }

$headerText = Get-Content -LiteralPath $headerPath -Raw
$problems = Test-Field $headerText (Get-Content -LiteralPath $listPath -Raw) $others
if ($problems.Count -gt 0) {
    Write-Host 'OVERLAY FIELDS CHECK FAILED:' -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}
$count = (Get-HeaderRead (Get-Code $headerText)).Reads.Count
Write-Host "overlay-fields-check OK - the Overlay reads $count members of the vendors' structs for Advanced metrics, each on the list; no camera position, axis or matrix in $($others.Count + 1) sources" -ForegroundColor Green
exit 0
