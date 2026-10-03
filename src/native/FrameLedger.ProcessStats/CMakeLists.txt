# FrameLedger.ProcessStats — the C ABI the managed Agent reads a game process's own memory through
# (beta.12, owner decision D43; docs/18_GPU_VENDOR_APIS.md §L1, docs/03_METRICS.md §Per-process memory).
#
# A SHARED library, because P/Invoke needs one and CLAUDE.md puts the native boundary in Infrastructure: the third
# facade after the guard's and the NVAPI bridge's. Read-only by construction — the video half is the documented
# `GPU Process Memory` performance counters (no handle to the game), the system half GetProcessMemoryInfo through a
# PROCESS_QUERY_LIMITED_INFORMATION handle; neither reads the game's memory.
#
# NEVER LOADED INTO A GAME. The Agent loads it beside its own binary by absolute path; nothing injects it, and its
# name carries none of the words the guard's §S18 heuristic scans a launcher's ancestors for.
#
# fl_native_flags only, NOT fl_hostile_env_flags: this runs in our own process. It ships, so
# tools/versioninfo-check.ps1 covers it.
add_library(FrameLedger.ProcessStats SHARED
    src/fl_process_stats.cpp
)
target_include_directories(FrameLedger.ProcessStats PUBLIC include)
target_link_libraries(FrameLedger.ProcessStats PRIVATE FrameLedger.Shm fl_native_flags pdh)
target_compile_definitions(FrameLedger.ProcessStats PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
fl_add_version_resource(FrameLedger.ProcessStats "FrameLedger process memory statistics (read-only; loaded by the Agent, never into a game)")
