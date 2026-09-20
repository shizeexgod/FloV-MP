cmake_minimum_required(VERSION 3.24)
project(FlovMpLegacy3889Adapter LANGUAGES CXX)

add_library(flovmp_legacy_native_3889 SHARED
    src/adapter.cpp
)

target_compile_features(flovmp_legacy_native_3889 PRIVATE cxx_std_20)
target_compile_definitions(flovmp_legacy_native_3889 PRIVATE NOMINMAX UNICODE _UNICODE)
target_link_libraries(flovmp_legacy_native_3889 PRIVATE bcrypt)
target_include_directories(flovmp_legacy_native_3889 PRIVATE include)

set_target_properties(flovmp_legacy_native_3889 PROPERTIES
    OUTPUT_NAME "flovmp-legacy-native-3889"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
)
