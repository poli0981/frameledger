// Check 4 — the static, pre-launch anti-cheat scan (docs/19_SAFETY §Pre-injection
// checks item 4, docs/05_DETECTION §Anti-cheat pre-scan).
//
// This check was DECLARED but never implemented. `Reason::kAntiCheatDirectory`
// and `kAntiCheatFile` existed, were named in ReasonName, and were mirrored into
// the managed enum — and nothing produced either, while two documents described
// the check as live and 14_TESTING specified a test for it. Three artifacts
// agreed on a behaviour no code had.
//
// Two rules shape this file:
//
//   1. IT RUNS INSIDE THE CHOKEPOINT. CheckStaticPreScan is called from
//      EvaluateImpl, not offered to a caller who might act on it. A pre-scan
//      that only advises the UI would be a check that gates nothing — and with
//      no persistence layer yet there is nowhere for such a verdict to be
//      stored, so `hook_blocked_reason` could not carry it either.
//   2. NO NEW MATCHING. Names are matched with fl::guard::MatchName against
//      Group::kDirectories and Group::kFiles — the same matcher, over the same
//      rules file, as the injection guard and the Vulkan layer. A second
//      matcher that can disagree with the first is a fail-open by construction
//      (§S15 item 1), and a family removed from the rules must stop firing
//      everywhere at once.

#ifndef FL_PRESCAN_H
#define FL_PRESCAN_H

#include <cstddef>
#include <fl_ac_rules.h>
#include <fl_guard.h>

namespace fl::guard {

// How far below the game directory we look, and how much we are willing to
// look at.
//
// Depth 2 was not arbitrary and was still too shallow. `EasyAntiCheat/` sits
// beside the executable and its EOS payloads sit one level inside it, which the
// old value covered — but "by 19_SAFETY's own table, deeper signals do not live
// there" was an assumption about the table, not about game installs.
//
// MEASURED 2026-08-04 (spike-notes §13): Neverness To Everness ships its own
// KERNEL DRIVER at `NTEGlobal/driver/PGameProtectDriver_X64.sys` — two
// directories below the install root, and therefore invisible. Adding the family
// to the blocklist changed nothing from the install root, which is where check 4
// actually runs; it only fired when the scan was started from `NTEGlobal`. A
// blocklist row that cannot be reached is coverage on paper.
//
// The cost was measured before the value moved, not assumed. Across 67 installed
// titles the worst case is 506 entries at the old reach and 729 at the new one,
// against kMaxPreScanEntries = 4096 — 18% of budget, 5.6x headroom. That matters
// because the entry cap is a REFUSAL: overrunning it does not scan less, it
// refuses the title.
//
// Both caps are REFUSALS, not truncations. A walk that stopped early has not
// seen the directory, and this file has no "scanned what we could" state.
inline constexpr std::size_t kMaxPreScanDepth = 3;
inline constexpr std::size_t kMaxPreScanEntries = 4096;

// Longest game directory path we will hold. MAX_PATH is not enough for a real
// Steam library on a long user name, and a truncated path is a path to somewhere
// else — so this is generous and overflow refuses.
inline constexpr std::size_t kMaxPreScanPathLen = 1024;

// Resolve a game's INSTALL ROOT from the directory its executable lives in.
//
// Not the same thing. Unreal puts the exe at <root>\<Project>\Binaries\Win64\,
// so scanning the executable's own directory looks at a folder containing the
// shipping binary and nothing else — and `EasyAntiCheat/` sits at the install
// root. MEASURED on Lies of P (2026-08-03): the exe is three levels below the
// root, and the pre-scan saw seven files, none of which could ever have been an
// anti-cheat SDK. For exactly the layout most likely to carry EAC, check 4 was
// looking in the wrong place.
//
// Walks up to a hardcoded platform boundary (`steamapps\common\<X>` and
// friends) and returns that game's folder. Boundaries are hardcoded for the same
// reason IsPlatformLauncher is: a data-driven boundary would let a rules update
// move where the hard gate looks.
//
// WHEN NO BOUNDARY IS RECOGNISED, `exeDir` IS RETURNED UNCHANGED. Walking up
// blindly is worse than staying put — one level above a game installed loose in
// `D:\games\Title\` is a folder of other games, and refusing this title because
// a sibling ships anti-cheat is a false refusal with no appeal.
//
// Returns false if the result does not fit, which the caller treats as
// "cannot determine".
[[nodiscard]] bool ResolveInstallRoot(const wchar_t* exeDir, wchar_t* out, std::size_t cap) noexcept;

// THE KERNEL-DRIVER RULE (owner decision 2026-09-25). Any `*.sys` file inside a game's install tree that no
// `files` entry names is reported as an anti-cheat file under this family, because a kernel driver shipped with a
// game is, measured on every case this project has met (PGameProtectDriver_X64.sys, randgrid.sys, NeacSafe64.sys,
// BlackCat64.sys, mhyprot*.sys), an anti-cheat driver — and naming each one in data first is how a new one walks
// past. It is CODE, not data: a rules file can neither remove it nor rename the family. A false positive turns the
// game's hooking off like any finding (19_SAFETY §What a finding does to the game); the owner took that trade.
inline constexpr const char* kKernelDriverInTreeFamily = "Kernel driver in the game folder";

// Check 4, as EvaluateImpl runs it: derive the target's directory from its pid,
// then scan it.
//
// Takes Sources because every evidence input in this guard is a seam — an input
// whose failure path cannot be exercised is an input whose failure path is
// unverified (fl_guard.h rule 3). It grants nothing: it opens no process with
// injection rights, reaches no injection primitive, and returns a Verdict its
// only caller already had to reach anyway.
//
// D33: with a `tolerance`, a file or folder of a tolerated family is recorded in `finding` and the walk goes on, so a
// kernel driver, another family or an unfinished walk further in still refuses. A `.sys` is never tolerated, whatever
// family names it. Without one (null), the check is exactly what it was.
[[nodiscard]] Verdict CheckStaticPreScan(const Sources& s, const Rules& rules, std::uint32_t targetPid,
                                         const Tolerance*  tolerance = nullptr,
                                         ToleratedFinding* finding = nullptr) noexcept;

// Check 3's store half over an install root: the store identity it carries (Sources::StoreIdentity) against
// `blockedStoreIds`. An install with no store identity is NOT refused — "not a store title" is an answer, and
// refusing every title that no store names would be a gate that cannot pass (§S14's matrix) — but a store layout
// whose metadata cannot be read is (kPreScanFailed): that one is "cannot determine". With nothing listed, nothing is
// read.
[[nodiscard]] Verdict CheckStoreIdentity(const Sources& s, const Rules& rules, const wchar_t* installRoot) noexcept;

// The same, as EvaluateImpl runs it: the install root derived from the pid, exactly as check 4 derives it.
[[nodiscard]] Verdict CheckBlockedStoreId(const Sources& s, const Rules& rules, std::uint32_t targetPid) noexcept;

// The advisory form, for the question "can this game be enabled at all?" (FR-2.2) and for the Agent's pre-scan of
// the library (19_SAFETY §What a finding does to the game). Same matcher, same polarity, no pid: check 3 (the
// executable's own name, and the store identity of its install root) and check 4 (the install root's tree), from the
// EXECUTABLE's path.
//
// It takes the executable, not a directory, since 2026-09-25. It took a directory, and its one caller passed the
// executable's own folder while nothing resolved the install root — so the pre-scan that decides whether hooking may
// be turned on looked where 19_SAFETY's Lies of P measurement says an Unreal title's EasyAntiCheat/ never is. The
// chokepoint always resolved the root; now this does too, from the same function.
//
// Loads the rules itself and uses SystemSources(), so a caller cannot choose the evidence — the same reason Evaluate
// and GuardedInject take no Sources.
//
// `toleratedFamilies` (D33), exactly as GuardedInject takes it: the verdict is kAllowedUserModeException when the only
// findings belonged to a tolerated user-mode family, which is how the Agent tells "this game would pass under its
// exception" from "it would not" without a second matcher.
[[nodiscard]] Verdict StaticPreScanGame(const wchar_t* exePath, const char* toleratedFamilies = nullptr) noexcept;

#ifdef FL_GUARD_TESTABLE
// TEST-ONLY, exactly as fl_guard.h defines the term: compiled out of everything
// that ships, and only src/native/tests may define the macro.
[[nodiscard]] Verdict StaticPreScanGameWithSources(const wchar_t* exePath, const Sources& sources,
                                                   const char* toleratedFamilies = nullptr) noexcept;
#endif

}    // namespace fl::guard

#endif    // FL_PRESCAN_H
