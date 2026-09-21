cmake_minimum_required(VERSION 3.20)
project(flovmp_client_3889 LANGUAGES CXX)

# Клиент FloV:MP для GTA V Legacy 1.0.3889.0 — ASI на ScriptHookV.
# ScriptHookV SDK для сборки не нужен: экспорты берутся во время работы,
# обёртки нативных функций — src/natives.h (tools/gen_natives.py).

set(IMGUI_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/imgui)

add_library(flovmp-client SHARED
    src/main.cpp
    src/common.cpp
    src/identity.cpp
    src/net.cpp
    src/game.cpp
    src/ui.cpp
    ${IMGUI_DIR}/imgui.cpp
    ${IMGUI_DIR}/imgui_draw.cpp
    ${IMGUI_DIR}/imgui_tables.cpp
    ${IMGUI_DIR}/imgui_widgets.cpp
    ${IMGUI_DIR}/backends/imgui_impl_dx11.cpp
)

target_compile_features(flovmp-client PRIVATE cxx_std_20)
target_include_directories(flovmp-client PRIVATE src ${IMGUI_DIR})
target_compile_definitions(flovmp-client PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE
    IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS)
if (MSVC)
    target_compile_options(flovmp-client PRIVATE /utf-8 /W3 /EHsc /GS /guard:cf)
    target_link_options(flovmp-client PRIVATE /GUARD:CF /DYNAMICBASE /NXCOMPAT)
    # Статический CRT: у игрока может не быть нужного VC++ Redistributable.
    set_property(TARGET flovmp-client PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()
target_link_libraries(flovmp-client PRIVATE ws2_32 bcrypt crypt32 iphlpapi version d3d11 d3dcompiler shell32 ole32)

set_target_properties(flovmp-client PROPERTIES
    OUTPUT_NAME "FloVMP"
    SUFFIX ".asi"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release"
)
