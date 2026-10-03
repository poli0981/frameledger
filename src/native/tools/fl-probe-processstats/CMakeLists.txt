# fl-probe-processstats — the instrument behind spike-notes §16 (beta.12, D43): what FrameLedger.ProcessStats.dll
# reads about a process, what it costs, and whether it agrees with the process's own QueryVideoMemoryInfo. A tool,
# not shipped; it builds with ordinary C++ (exceptions on) because it is our own process and uses the STL freely.
add_executable(fl-probe-processstats main.cpp)
target_link_libraries(fl-probe-processstats PRIVATE FrameLedger.ProcessStats fl_native_flags)
target_compile_options(fl-probe-processstats PRIVATE /EHsc)
target_compile_definitions(fl-probe-processstats PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE)
add_custom_command(TARGET fl-probe-processstats POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:FrameLedger.ProcessStats> $<TARGET_FILE_DIR:fl-probe-processstats>)
