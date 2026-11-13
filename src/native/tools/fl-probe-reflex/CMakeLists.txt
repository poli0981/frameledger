# fl-probe-reflex -- the instrument of docs/spike-notes §31 (Reflex route D, owner decision D132). A tool, NOT
# SHIPPED, and it touches no game.
#
# BUILT WITH THE OVERLAY'S OWN FLAGS (fl_hostile_env_flags), as fl-probe-hookprofile is, AND FROM THE OVERLAY'S OWN
# HEADER: the call sequence is FrameLedger.Overlay/include/fl_reflex.h since the owner said go (D136), and this probe
# includes it. So its ctest runs what ships, with Control Flow Guard enforced -- which hook-harness, the target of
# the injected tests, does not enforce.
#
# THE NVAPI HEADERS, NOT THE NVAPI LIBRARY. The include directory of the vendored MIT headers is taken by itself;
# fl_nvapi -- which also links nvapi64.lib -- is deliberately not: the static library loads nvapi64.dll itself, from
# a trusted location, with a signature check, and route D's shape is that the process already has the module.
add_executable(fl-probe-reflex main.cpp)
target_link_libraries(fl-probe-reflex PRIVATE fl_native_flags fl_hostile_env_flags d3d11 d3d12 dxgi)
target_link_libraries(fl-probe-reflex PRIVATE fl_nvapi_headers)
target_include_directories(fl-probe-reflex PRIVATE "${CMAKE_SOURCE_DIR}/FrameLedger.Overlay/include")
target_compile_definitions(fl-probe-reflex PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE)
add_dependencies(fl-probe-reflex fl_stub_nvapi64)

# The half a build machine can run: the candidate against the stand-in module -- absent, without its getters, a
# report of nothing, an error, sixty-four reports, both device kinds, a call that does not come back for a while.
# Software devices; exit 77 where there is no Direct3D 11 at all. `--real` (the installed driver, an NVIDIA adapter)
# is a measurement the note records, never a test.
add_test(NAME fl_reflex_probe_stub COMMAND fl-probe-reflex --stub "$<TARGET_FILE:fl_stub_nvapi64>")
set_tests_properties(fl_reflex_probe_stub PROPERTIES SKIP_RETURN_CODE 77)
# Its last line when nothing failed (a run cut short is not a pass -- the top-level file, above enable_testing()).
fl_test_completes(fl_reflex_probe_stub "STUB OK: 0 failure\\(s\\)")
