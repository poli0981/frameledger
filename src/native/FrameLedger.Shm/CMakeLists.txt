# Header-only: the shared-memory contract between the Overlay (inside the game)
# and the Agent (outside it). Exactly one definition, no link dependency.
add_library(FrameLedger.Shm INTERFACE)
target_include_directories(FrameLedger.Shm INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
target_compile_features(FrameLedger.Shm INTERFACE cxx_std_20)

# FL_BUILD_ID — the producer FlShmHandshake::buildId never had.
#
# 07_IPC and 04_CAPTURE make a buildId mismatch a hard refuse-to-attach, and
# 17_HOOK_ENGINE requires an FlGetBuildId() export. Nothing wrote the field:
# grep found the declaration, its offset assert and the layout dump, and nothing
# else. A check whose input nobody produces compares "" with "" forever, which is
# a gate that cannot fail wearing a version-skew costume.
#
# git describe, because the question the field answers is "which build is this
# DLL" and a commit answers it exactly. Falls back to a literal rather than to an
# empty string: "unknown" is a value the mismatch check can act on, "" is the
# state that made the check vacuous.
#
# Deliberately NOT a timestamp. Two builds of the same commit must compare equal,
# or every rebuild would tell the user to restart their game.
#
# KNOWN LIMIT, stated rather than discovered: this runs at CONFIGURE time, so the
# `-dirty` suffix reflects the tree when CMake last ran, not when the DLL was
# last compiled. This comment said that for a released build it was exact, "CI
# configures and builds once from a clean checkout", and it was not (corrected
# 2026-10-07, beta.18): release.yml writes the release date into the tracked
# legal/*.md before the gate configures, and every release until beta.18 shipped
# "<tag>-dirty" — the owner's Dashboard read "Overlay build v0.1.0-beta.17-dirty",
# and the run's log "FL_BUILD_ID = v0.1.0-beta.17-dirty" two lines after
# "RELEASE_DATE = 2026-10-07 in 3 file(s)". A release now hands the id in (below). For a
# developer editing between configures it is approximate, and the field is a
# version-skew signal rather than a provenance guarantee. Making it exact means
# re-running git on every build, which is a cost on every incremental compile for a
# value only the attach handshake reads.
#
# A RELEASE HANDS THE ID IN: release.yml reads describe from the clean checkout, before
# anything edits a tracked file, and passes it as the environment's FL_BUILD_ID; it is
# taken as given here, and tools/buildid-check.ps1 then reads it back out of the
# published binaries. It goes into a C string literal, so it must be what describe can
# print and nothing a literal would have to escape.
if(DEFINED ENV{FL_BUILD_ID} AND NOT "$ENV{FL_BUILD_ID}" STREQUAL "")
    set(FL_BUILD_ID_RAW "$ENV{FL_BUILD_ID}")
    if(NOT FL_BUILD_ID_RAW MATCHES "^[0-9A-Za-z._+-]+$")
        message(FATAL_ERROR "FL_BUILD_ID '${FL_BUILD_ID_RAW}' from the environment holds a character no build id has (0-9 A-Z a-z . _ + -).")
    endif()
    string(LENGTH "${FL_BUILD_ID_RAW}" FL_BUILD_ID_LEN)
else()
    execute_process(
        COMMAND git describe --always --dirty --abbrev=12
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        OUTPUT_VARIABLE FL_BUILD_ID_RAW
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    if(NOT FL_BUILD_ID_RAW)
        set(FL_BUILD_ID_RAW "unknown")
    endif()
    # buildId is char[32] and the guard side compares the whole array, so a value
    # that would be truncated must be caught here rather than becoming a permanent
    # mismatch nobody can explain.
    string(LENGTH "${FL_BUILD_ID_RAW}" FL_BUILD_ID_LEN)
    # AFTER THE FIRST TAG the long form stopped fitting on a developer's machine: `v0.1.0-beta.2-2-gd3e74988a7bc-dirty`
    # is 35 characters, so every local configure with an uncommitted change failed here and the documented workaround
    # was "commit before you build" (measured 2026-09-17, again 2026-09-21). The id is a version-skew signal, not a
    # provenance guarantee (above), so the fallback spends fewer characters on the same facts: an 8-digit abbreviation
    # and `+` for dirty. A released build hands its id in (above) and never reaches this branch. Still too long
    # after that is still fatal, for the reason the check exists.
    if(FL_BUILD_ID_LEN GREATER 31)
        execute_process(
            COMMAND git describe --always --dirty=+ --abbrev=8
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
            OUTPUT_VARIABLE FL_BUILD_ID_RAW
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        string(LENGTH "${FL_BUILD_ID_RAW}" FL_BUILD_ID_LEN)
    endif()
endif()
if(FL_BUILD_ID_LEN GREATER 31)
    message(FATAL_ERROR
        "FL_BUILD_ID '${FL_BUILD_ID_RAW}' is ${FL_BUILD_ID_LEN} chars; FlShmHandshake::buildId holds 31 + NUL. "
        "Truncating it would make every attach fail on a mismatch that is really this build system's fault.")
endif()
target_compile_definitions(FrameLedger.Shm INTERFACE FL_BUILD_ID="${FL_BUILD_ID_RAW}")
message(STATUS "FL_BUILD_ID = ${FL_BUILD_ID_RAW}")
