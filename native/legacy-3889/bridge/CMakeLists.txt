cmake_minimum_required(VERSION 3.20)
project(flovmp_legacy_3889_bridge LANGUAGES CXX)

add_library(flovmp-legacy-3889-bridge SHARED src/bridge.cpp)
target_compile_features(flovmp-legacy-3889-bridge PRIVATE cxx_std_20)
target_compile_definitions(flovmp-legacy-3889-bridge PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(flovmp-legacy-3889-bridge PRIVATE ws2_32)
set_target_properties(flovmp-legacy-3889-bridge PROPERTIES
    OUTPUT_NAME "flovmp-legacy-3889-bridge"
    SUFFIX ".asi"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release"
)
