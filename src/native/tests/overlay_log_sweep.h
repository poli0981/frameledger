// Test-only. The hook-harness's overlay logs, removed when a test binary's run ends.
//
// The Overlay writes %LOCALAPPDATA%\FrameLedger\logs\overlay-<pid>-<stamp>.log: the real per-user data folder,
// resolved by SHGetKnownFolderPath and deliberately NOT by the environment (§S21), and the shipped DLL is the one
// under test, so a test cannot point it anywhere else and the DLL gets no switch that could. Measured 2026-09-15:
// that folder held 2506 overlay logs, 2492 of them hook-harness's, left by test runs since 2026-09-06.
//
// So a test binary removes, when its run ends, exactly what it caused: a log CREATED at or after the run started
// whose first line names this build's hook-harness as the image. A game's log names the game and is never touched;
// a log from an earlier run predates the start and stays, the owner's to delete (17_HOOK_ENGINE §Native logging).
#ifndef FL_OVERLAY_LOG_SWEEP_H
#define FL_OVERLAY_LOG_SWEEP_H

#include <windows.h>

#include <fl_ac_rules.h>
#include <string>

namespace fl::testing {

struct SweepCount {
    int removed = 0;
    int kept = 0;    // created during the run but not a harness log, or could not be removed
};

// "# FrameLedger.Overlay build <id> pid <n> layout v<k> image <path>" (dllmain.cpp FlushLog) -> <path>.
inline bool OverlayLogImage(const std::string& firstLine, std::string& image) {
    static const char kPrefix[] = "# FrameLedger.Overlay build ";
    if (firstLine.rfind(kPrefix, 0) != 0) {
        return false;
    }
    const std::string::size_type at = firstLine.find(" image ");
    if (at == std::string::npos) {
        return false;
    }
    image = firstLine.substr(at + 7);
    while (!image.empty() && (image.back() == '\r' || image.back() == '\n' || image.back() == ' ')) {
        image.pop_back();
    }
    return !image.empty();
}

// The header's image comes from GetModuleFileNameA, so it is in the ANSI code page.
inline std::wstring WidenAnsi(const std::string& text) {
    if (text.empty()) {
        return {};
    }
    const int n = MultiByteToWideChar(CP_ACP, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
    if (n <= 0) {
        return {};
    }
    std::wstring out(static_cast<std::size_t>(n), L'\0');
    MultiByteToWideChar(CP_ACP, 0, text.data(), static_cast<int>(text.size()), out.data(), n);
    return out;
}

// CMake's $<TARGET_FILE> spells '/', GetModuleFileNameA spells '\'; neither separator nor case tells two paths apart.
inline bool SamePath(std::wstring a, std::wstring b) {
    for (wchar_t& ch : a) {
        if (ch == L'/') {
            ch = L'\\';
        }
    }
    for (wchar_t& ch : b) {
        if (ch == L'/') {
            ch = L'\\';
        }
    }
    return CompareStringOrdinal(a.c_str(), static_cast<int>(a.size()), b.c_str(), static_cast<int>(b.size()), TRUE) ==
           CSTR_EQUAL;
}

// The first line of a file, from its first 1 KiB; empty when it cannot be read.
inline std::string FirstLine(const std::wstring& path) {
    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) {
        return {};
    }
    char       buf[1024];
    DWORD      n = 0;
    const BOOL ok = ReadFile(h, buf, sizeof(buf), &n, nullptr);
    CloseHandle(h);
    if (!ok) {
        return {};
    }
    const std::string            text(buf, n);
    const std::string::size_type nl = text.find('\n');
    return nl == std::string::npos ? text : text.substr(0, nl);
}

// Removes every overlay-*.log directly under `logsDir` that was created at or after `since` and whose header names
// `harness` as the image. Anything else is left where it is.
inline SweepCount SweepHarnessLogs(const std::wstring& logsDir, const FILETIME& since, const std::wstring& harness) {
    SweepCount         count;
    const std::wstring pattern = logsDir + L"\\overlay-*.log";
    WIN32_FIND_DATAW   fd{};
    HANDLE             find = FindFirstFileW(pattern.c_str(), &fd);
    if (find == INVALID_HANDLE_VALUE) {
        return count;
    }
    do {
        if ((fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 || CompareFileTime(&fd.ftCreationTime, &since) < 0) {
            continue;
        }
        const std::wstring path = logsDir + L"\\" + fd.cFileName;
        std::string        image;
        if (!OverlayLogImage(FirstLine(path), image) || !SamePath(WidenAnsi(image), harness)) {
            ++count.kept;
            continue;
        }
        if (DeleteFileW(path.c_str())) {
            ++count.removed;
        } else {
            ++count.kept;
        }
    } while (FindNextFileW(find, &fd));
    FindClose(find);
    return count;
}

// %LOCALAPPDATA%\FrameLedger\logs, resolved the way the Overlay resolves it.
inline bool RealOverlayLogsDir(std::wstring& out) {
    wchar_t base[fl::guard::kMaxRulesPathLen]{};
    if (!fl::guard::LocalAppDataDir(base, fl::guard::kMaxRulesPathLen)) {
        return false;
    }
    out = std::wstring(base) + L"\\FrameLedger\\logs";
    return true;
}

}    // namespace fl::testing

#endif    // FL_OVERLAY_LOG_SWEEP_H
