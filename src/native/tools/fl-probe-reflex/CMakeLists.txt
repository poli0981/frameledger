# fl-probe-reflex -- the instrument of docs/spike-notes §31 (Reflex route D, owner decision D132). A tool, NOT
# SHIPPED, and it touches no game.
#
# BUILT WITH THE OVERLAY'S OWN FLAGS (fl_hostile_env_flags), as fl-probe-hookprofile is: fl_reflex_candidate.h is the
# call sequence that would move into the Overlay if the owner says go, and a probe built otherwise would prove
# nothing about it there.
#
# THE NVAPI HEADERS, NOT THE NVAPI LIBRARY. The include directory of the vendored MIT headers is taken by itself;
# fl_nvapi -- which also links nvapi64.lib -- is deliberately not: the static library loads nvapi64.dll itself, from
# a trusted location, with a signature check, and route D's shape is that the process already has the module.
add_executable(fl-probe-reflex main.cpp)
target_link_libraries(fl-probe-reflex PRIVATE fl_native_flags fl_hostile_env_flags d3d11 d3d12 dxgi)
target_include_directories(fl-probe-reflex SYSTEM PRIVATE "${CMAKE_SOURCE_DIR}/third_party/nvapi")
target_compile_definitions(fl-probe-reflex PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE)
add_dependencies(fl-probe-reflex fl_stub_nvapi64)

# The half a build machine can run: the candidate against the stand-in module -- absent, without its getters, a
# report of nothing, an error, sixty-four reports, both device kinds, a call that does not come back for a while.
# Software devices; exit 77 where there is no Direct3D 11 at all. `--real` (the installed driver, an NVIDIA adapter)
# is a measurement the note records, never a test.
add_test(NAME fl_reflex_probe_stub COMMAND fl-probe-reflex --stub "$<TARGET_FILE:fl_stub_nvapi64>")
set_tests_properties(fl_reflex_probe_stub PROPERTIES SKIP_RETURN_CODE 77)
