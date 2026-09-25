// D33 (owner decision 2026-09-26) — which anti-cheat families the injected Overlay may see load WITHOUT stopping.
//
// The Overlay stops observing when a module the compiled floor names loads after injection (its
// kernelbase!LoadLibraryExW detour, dllmain.cpp). Under a user's exception for one game (19_SAFETY §The user-mode
// exception) the guard lets that game's one user-mode family through, and the Overlay must know which, or the exception
// does nothing the moment the family's module loads late. The ring cannot carry it: the Overlay CREATES the ring
// (fl_shm_host.h) and the Agent opens it afterwards, so nothing the Agent writes there exists before the detour is
// armed.
//
// So the Agent creates this small mapping BEFORE it asks the guard to inject, holds it for the session, and the
// Overlay's init thread reads it ONCE, before it installs the detour. It carries NAMES, never indices: the Overlay
// resolves each against its own compiled floor and honours only a family whose whole footprint there is user-mode
// (fl::guard::FamilyIsUserModeOnly), so a mapping that named a kernel-level family — by mistake or by anyone else
// who can create objects in this session — tolerates nothing. It only ever narrows the in-process stop for families
// the guard itself would tolerate; the Agent's 30 s re-scan still refuses everything else.
//
// Absent mapping = no tolerance: every game without an exception, and every game before D33, behaves exactly as before.
//
// Mirrored in FrameLedger.Shared (ShmLayout.cs, FlTolerance) and held to it by ShmLayoutMirrorTests through
// tools/fl-layout-dump, as every struct that crosses the native/managed line is.

#ifndef FRAMELEDGER_FL_TOLERANCE_H
#define FRAMELEDGER_FL_TOLERANCE_H

#include <cstddef>
#include <cstdint>
#include <cwchar>

// "FLTL", little-endian: a mapping whose first word is not this was not written by the Agent for this purpose.
inline constexpr std::uint32_t FL_TOLERANCE_MAGIC = 0x4C544C46u;
inline constexpr std::uint32_t FL_TOLERANCE_VERSION = 1u;
inline constexpr std::uint32_t FL_TOLERANCE_MAX_FAMILIES = 8u;    // fl::guard::kMaxToleratedFamilies
inline constexpr std::uint32_t FL_TOLERANCE_NAME_LEN = 64u;       // fl::guard::kMaxFamilyNameLen

struct FlTolerance {
    std::uint32_t magic;       // @0  FL_TOLERANCE_MAGIC
    std::uint32_t version;     // @4  FL_TOLERANCE_VERSION
    std::uint32_t count;       // @8  names used, <= FL_TOLERANCE_MAX_FAMILIES
    std::uint32_t reserved;    // @12 must be zero
    char          families[FL_TOLERANCE_MAX_FAMILIES][FL_TOLERANCE_NAME_LEN];    // @16 NUL-terminated ASCII names
};

static_assert(offsetof(FlTolerance, families) == 16, "FlTolerance: the names start at 16");
static_assert(sizeof(FlTolerance) == 16 + FL_TOLERANCE_MAX_FAMILIES * FL_TOLERANCE_NAME_LEN,
              "FlTolerance: no implicit padding");

// Local\FrameLedger.Tolerate.<pid> — the pid the Overlay is loaded into, i.e. the game's.
inline bool MakeToleranceName(wchar_t* out, std::size_t cap, unsigned long pid) noexcept {
    return _snwprintf_s(out, cap, _TRUNCATE, L"Local\\FrameLedger.Tolerate.%lu", pid) >= 0;
}

#endif    // FRAMELEDGER_FL_TOLERANCE_H
