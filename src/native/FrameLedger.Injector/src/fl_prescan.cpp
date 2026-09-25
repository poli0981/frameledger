// windows.h stays in its own block above the sorted group, as fl_ac_rules.cpp does: WideCharToMultiByte (the one
// Win32 call here, for the executable's leaf name) needs it.
#include <windows.h>

#include <cstring>
#include <cwchar>
#include <fl_prescan.h>

namespace fl::guard {
namespace {

// Mirrors the Refuse/Allow pair in fl_guard.cpp. Duplicated rather than shared
// because exporting them would put verdict construction on the public surface,
// and a Verdict a caller can mint is one step from a clearance a caller can
// mint (§S13(b)).
Verdict Refused(Reason r, const char* family, const char* signal) noexcept {
    Verdict v;
    v.reason = r;
    if (family != nullptr) {
        strncpy_s(v.family, sizeof(v.family), family, _TRUNCATE);
    }
    if (signal != nullptr) {
        strncpy_s(v.signal, sizeof(v.signal), signal, _TRUNCATE);
    }
    return v;
}

Verdict Passed() noexcept {
    Verdict v;
    v.reason = Reason::kAllow;
    return v;
}

// True for a name ending in ".sys", ASCII case-insensitively. Names reach the sink as UTF-8 and the suffix is ASCII,
// so a byte comparison of the last four characters is exact.
bool IsKernelDriverName(const char* name) noexcept {
    const std::size_t n = std::strlen(name);
    return n > 4 && _stricmp(name + n - 4, ".sys") == 0;
}

// What the sink accumulates. No allocation: the walk reports names one at a
// time and we keep only the first hit.
struct ScanState {
    const Rules*  rules = nullptr;
    const Family* hit = nullptr;
    const char*   driverRuleHit = nullptr;    // kKernelDriverInTreeFamily once the code rule fired
    bool          hitWasDirectory = false;
    char          signal[260] = {};

    // D33: the exception's families, and the first entry of theirs the walk let through.
    const Tolerance* tolerance = nullptr;
    const Family*    tolerated = nullptr;
    char             toleratedSignal[260] = {};
};

bool EntrySink(void* ctx, const char* name, bool isDirectory) noexcept {
    auto* st = static_cast<ScanState*>(ctx);
    if (st == nullptr || st->rules == nullptr || name == nullptr || name[0] == '\0') {
        // A nameless entry is one we could not inspect. Keep walking — the
        // enumerator reports that as kIncomplete, which refuses on its own; if
        // we stopped here we would refuse with the wrong reason.
        return true;
    }

    // Directories and files are matched against DIFFERENT groups. Group
    // membership is load-bearing (fl_ac_rules.h): `EasyAntiCheat` as a directory
    // and `x3.xem` as a file are separate signals, and collapsing them would let
    // a data edit move one gate into the other without anything noticing.
    const Group   group = isDirectory ? Group::kDirectories : Group::kFiles;
    const Family* tolerated = nullptr;
    const Family* f = st->tolerance != nullptr
                          ? MatchNameTolerating(*st->rules, group, name, *st->tolerance, &tolerated)
                          : MatchName(*st->rules, group, name);

    // D33. A KERNEL DRIVER IS NEVER TOLERATED, whichever family names it: FamilyIsUserModeOnly already refuses a family
    // whose own values carry a `.sys`, and this closes the one way left - a prefix value of a user-mode family that
    // happens to match a driver's file name. That driver refuses under the family, exactly as without an exception.
    if (f == nullptr && tolerated != nullptr && !isDirectory && IsKernelDriverName(name)) {
        f = tolerated;
    }
    if (f == nullptr && tolerated != nullptr) {
        // Let through, recorded, and the walk goes ON: a kernel driver, another family or an unfinished listing
        // further in must still refuse. The walk is bounded by kMaxPreScanEntries either way (a refusal).
        if (st->tolerated == nullptr) {
            st->tolerated = tolerated;
            strncpy_s(st->toleratedSignal, sizeof(st->toleratedSignal), name, _TRUNCATE);
        }
        return true;
    }
    if (f != nullptr) {
        st->hit = f;
        st->hitWasDirectory = isDirectory;
        strncpy_s(st->signal, sizeof(st->signal), name, _TRUNCATE);
        return false;    // stop: we have the answer
    }

    // The kernel-driver rule (fl_prescan.h). A `files` entry that names the driver wins above, so a driver the data
    // knows keeps its own family; any other `*.sys` in the tree is reported under the code rule's family.
    if (!isDirectory && IsKernelDriverName(name)) {
        st->driverRuleHit = kKernelDriverInTreeFamily;
        strncpy_s(st->signal, sizeof(st->signal), name, _TRUNCATE);
        return false;
    }
    return true;
}

Verdict ScanWith(const Sources& s, const Rules& rules, const wchar_t* dir, const Tolerance* tolerance,
                 ToleratedFinding* finding) noexcept {
    if (s.EnumerateDirEntries == nullptr) {
        return Refused(Reason::kPreScanFailed, nullptr, "no directory source");
    }
    if (dir == nullptr || dir[0] == L'\0') {
        return Refused(Reason::kPreScanFailed, nullptr, "no game directory to scan");
    }

    ScanState st;
    st.rules = &rules;
    st.tolerance = tolerance;

    const Collected c = s.EnumerateDirEntries(dir, &EntrySink, &st);

    // The hit is reported even if the walk then reported a problem: a directory
    // we positively identified is a stronger signal than an incomplete listing,
    // and both refuse anyway.
    if (st.hit != nullptr) {
        return Refused(st.hitWasDirectory ? Reason::kAntiCheatDirectory : Reason::kAntiCheatFile, st.hit->name,
                       st.signal);
    }
    if (st.driverRuleHit != nullptr) {
        return Refused(Reason::kAntiCheatFile, st.driverRuleHit, st.signal);
    }

    // EVERY uncertainty is a refusal, and each has its own text because "the
    // directory is gone" and "there were more entries than we will look at" are
    // different problems for whoever has to fix them.
    if (c == Collected::kFailed) {
        return Refused(Reason::kPreScanFailed, nullptr, "the game directory could not be listed");
    }
    if (c == Collected::kIncomplete) {
        return Refused(Reason::kPreScanFailed, nullptr,
                       "the game directory listing was truncated, unreadable, or crossed a reparse point");
    }
    // The whole tree was seen and only the exception's family was in it: the caller turns the record into the verdict.
    if (st.tolerated != nullptr && finding != nullptr && !finding->seen) {
        finding->seen = true;
        strncpy_s(finding->family, sizeof(finding->family), st.tolerated->name, _TRUNCATE);
        strncpy_s(finding->signal, sizeof(finding->signal), st.toleratedSignal, _TRUNCATE);
    }
    return Passed();
}

// Case-insensitive comparison of one path segment.
bool SegmentIs(const wchar_t* begin, const wchar_t* end, const wchar_t* literal) noexcept {
    const std::size_t n = static_cast<std::size_t>(end - begin);
    return wcslen(literal) == n && _wcsnicmp(begin, literal, n) == 0;
}

// The rules the advisory pre-scan reads, loaded as LoadRules loads them for the chokepoint: the one rules location,
// the one parser, every failure a refusal with its own reason. Static rather than stack for LoadRules' reason too:
// ~1 MiB of text and ~530 KB of parsed rules, and the guard is not re-entrant.
Verdict LoadPreScanRules(const Sources& s, Rules& rules) noexcept {
    if (s.ReadRulesFile == nullptr) {
        return Refused(Reason::kRulesUnreadable, nullptr, "no rules source");
    }
    static char       buffer[kMaxRulesBytes];
    const std::size_t n = s.ReadRulesFile(buffer, sizeof(buffer));
    if (n == static_cast<std::size_t>(-1) || n == 0) {
        return Refused(Reason::kRulesUnreadable, nullptr, "rules file could not be read");
    }
    switch (ParseRules(buffer, n, rules)) {
    case ParseResult::kOk:
        return Passed();
    case ParseResult::kIncomplete:
        return Refused(Reason::kRulesIncomplete, nullptr, "a required anti-cheat family is missing");
    case ParseResult::kTooLarge:
        return Refused(Reason::kRulesMalformed, nullptr, "rules file exceeds the parser's bounds");
    case ParseResult::kMalformed:
    default:
        return Refused(Reason::kRulesMalformed, nullptr, "rules file is not the shape the guard requires");
    }
}

// D33's verdict: every check passed and the only findings were the exception's. Built here and in fl_guard.cpp from
// the same record, and nowhere else.
Verdict ToleratedVerdict(const ToleratedFinding& finding) noexcept {
    return Refused(Reason::kAllowedUserModeException, finding.family, finding.signal);
}

// The advisory pre-scan over whichever Sources the caller may hold (production: SystemSources, and nothing else).
Verdict PreScanGameWith(const wchar_t* exePath, const Sources& s, const char* toleratedFamilies) noexcept {
    if (exePath == nullptr || exePath[0] == L'\0') {
        return Refused(Reason::kPreScanFailed, nullptr, "no executable to scan");
    }

    // Split the path into its directory and its leaf. A path with no separator is not a path we understand.
    wchar_t dir[kMaxPreScanPathLen] = {};
    if (wcscpy_s(dir, kMaxPreScanPathLen, exePath) != 0) {
        return Refused(Reason::kPreScanFailed, nullptr, "the executable's path is longer than the scan will hold");
    }
    wchar_t* lastSep = nullptr;
    for (wchar_t* p = dir; *p != L'\0'; ++p) {
        if (*p == L'\\' || *p == L'/') {
            lastSep = p;
        }
    }
    if (lastSep == nullptr || lastSep == dir || lastSep[1] == L'\0') {
        return Refused(Reason::kPreScanFailed, nullptr, "the executable's path names no file in a directory");
    }
    const wchar_t* leafWide = exePath + (lastSep - dir) + 1;
    *lastSep = L'\0';

    wchar_t root[kMaxPreScanPathLen] = {};
    if (!ResolveInstallRoot(dir, root, kMaxPreScanPathLen)) {
        return Refused(Reason::kPreScanFailed, nullptr, "could not establish the game's install root");
    }

    static Rules rules;
    if (Verdict v = LoadPreScanRules(s, rules); !v.Allowed()) {
        return v;
    }

    // Check 3, the executable half. The exact-conversion rule ImageFileNameImpl follows: a name we cannot represent
    // exactly is a name we could not compare, never a clean miss.
    char      leaf[kMaxValueLen] = {};
    const int written = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, leafWide, -1, leaf,
                                            static_cast<int>(sizeof(leaf)), nullptr, nullptr);
    if (written <= 0) {
        return Refused(Reason::kPreScanFailed, nullptr, "the executable's name could not be read exactly");
    }
    if (const TitleRule* hit = MatchesBlockedExecutable(rules, leaf); hit != nullptr) {
        return Refused(Reason::kBlockedExecutable, hit->family, leaf);
    }

    // Check 3's store half, then check 4 — both over the install root. Title lists are never tolerated (D33): only
    // check 4 takes the exception, and only for a user-mode family.
    if (Verdict v = CheckStoreIdentity(s, rules, root); !v.Allowed()) {
        return v;
    }
    Tolerance tolerance;
    ResolveTolerance(rules, toleratedFamilies, tolerance);
    ToleratedFinding finding;
    if (Verdict v = ScanWith(s, rules, root, tolerance.any ? &tolerance : nullptr, &finding); !v.Allowed()) {
        return v;
    }
    return finding.seen ? ToleratedVerdict(finding) : Passed();
}

}    // namespace

bool ResolveInstallRoot(const wchar_t* exeDir, wchar_t* out, std::size_t cap) noexcept {
    if (exeDir == nullptr || out == nullptr || cap == 0) {
        return false;
    }

    // Split into segments without allocating.
    constexpr std::size_t kMaxSegments = 64;
    const wchar_t*        starts[kMaxSegments] = {};
    const wchar_t*        ends[kMaxSegments] = {};
    std::size_t           count = 0;

    const wchar_t* p = exeDir;
    while (*p != L'\0' && count < kMaxSegments) {
        while (*p == L'\\' || *p == L'/') {
            ++p;
        }
        if (*p == L'\0') {
            break;
        }
        starts[count] = p;
        while (*p != L'\0' && *p != L'\\' && *p != L'/') {
            ++p;
        }
        ends[count] = p;
        ++count;
    }

    // Boundaries whose CHILD is the install root. Hardcoded, per the header.
    struct Boundary {
        const wchar_t* seg[2];
        std::size_t    len;
    };
    static const Boundary kBoundaries[] = {
        {{L"steamapps", L"common"}, 2},
        {{L"GOG Galaxy", L"Games"}, 2},
        {{L"Epic Games", nullptr}, 1},
    };

    for (const Boundary& b : kBoundaries) {
        for (std::size_t i = 0; i + b.len < count; ++i) {
            bool hit = true;
            for (std::size_t k = 0; k < b.len && hit; ++k) {
                hit = SegmentIs(starts[i + k], ends[i + k], b.seg[k]);
            }
            if (!hit) {
                continue;
            }
            // Root ends at the segment AFTER the boundary.
            const wchar_t* rootEnd = ends[i + b.len];
            const auto     n = static_cast<std::size_t>(rootEnd - exeDir);
            if (n + 1 > cap) {
                return false;    // a truncated path is a path to somewhere else
            }
            std::wmemcpy(out, exeDir, n);
            out[n] = L'\0';
            return true;
        }
    }

    // No boundary recognised: the executable's own directory, unchanged.
    if (wcslen(exeDir) + 1 > cap) {
        return false;
    }
    wcscpy_s(out, cap, exeDir);
    return true;
}

Verdict CheckStaticPreScan(const Sources& s, const Rules& rules, std::uint32_t targetPid, const Tolerance* tolerance,
                           ToleratedFinding* finding) noexcept {
    if (s.ImageDirectory == nullptr) {
        return Refused(Reason::kPreScanFailed, nullptr, "no image-path source");
    }

    wchar_t dir[kMaxPreScanPathLen] = {};
    if (s.ImageDirectory(targetPid, dir, kMaxPreScanPathLen) != Collected::kOk || dir[0] == L'\0') {
        // We could not find out where the game lives, so check 4 did not run.
        // 19_SAFETY has no "three of four checks passed" state.
        return Refused(Reason::kPreScanFailed, nullptr, "could not establish the target's directory");
    }
    return ScanWith(s, rules, dir, tolerance, finding);
}

Verdict CheckStoreIdentity(const Sources& s, const Rules& rules, const wchar_t* installRoot) noexcept {
    // Nothing listed, nothing to compare: the store's files are not opened at all.
    if (rules.blockedStoreIdCount == 0) {
        return Passed();
    }
    if (s.StoreIdentity == nullptr) {
        return Refused(Reason::kPreScanFailed, nullptr, "no store-identity source");
    }
    if (installRoot == nullptr || installRoot[0] == L'\0') {
        return Refused(Reason::kPreScanFailed, nullptr, "no install root to read a store identity from");
    }

    char id[kMaxValueLen] = {};
    if (s.StoreIdentity(installRoot, id, sizeof(id)) != Collected::kOk) {
        return Refused(Reason::kPreScanFailed, nullptr, "the store's metadata for this install could not be read");
    }
    if (id[0] == '\0') {
        return Passed();    // no store names this install: check 3's store half does not apply (§S14's matrix)
    }
    if (const TitleRule* hit = MatchesBlockedStoreId(rules, id); hit != nullptr) {
        return Refused(Reason::kBlockedStoreId, hit->family, id);
    }
    return Passed();
}

Verdict CheckBlockedStoreId(const Sources& s, const Rules& rules, std::uint32_t targetPid) noexcept {
    if (rules.blockedStoreIdCount == 0) {
        return Passed();
    }
    if (s.ImageDirectory == nullptr) {
        return Refused(Reason::kPreScanFailed, nullptr, "no image-path source");
    }
    wchar_t root[kMaxPreScanPathLen] = {};
    if (s.ImageDirectory(targetPid, root, kMaxPreScanPathLen) != Collected::kOk || root[0] == L'\0') {
        return Refused(Reason::kPreScanFailed, nullptr, "could not establish the target's directory");
    }
    return CheckStoreIdentity(s, rules, root);
}

Verdict StaticPreScanGame(const wchar_t* exePath, const char* toleratedFamilies) noexcept {
    return PreScanGameWith(exePath, SystemSources(), toleratedFamilies);
}

#ifdef FL_GUARD_TESTABLE
Verdict StaticPreScanGameWithSources(const wchar_t* exePath, const Sources& sources,
                                     const char* toleratedFamilies) noexcept {
    return PreScanGameWith(exePath, sources, toleratedFamilies);
}
#endif

}    // namespace fl::guard
