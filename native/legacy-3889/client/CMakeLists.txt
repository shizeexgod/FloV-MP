cmake_minimum_required(VERSION 3.20)
project(flovmp_client_3889 LANGUAGES C CXX RC)

# Клиент FloV:MP для GTA V Legacy 1.0.3889.0 — ASI на ScriptHookV.
# ScriptHookV SDK для сборки не нужен: экспорты берутся во время работы,
# обёртки нативных функций — src/natives.h (tools/gen_natives.py).

set(IMGUI_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/imgui)

# Движок JS для клиентского кода сервера (пункт 26): quickjs-ng, MIT. Только
# ядро — без модулей std/os, то есть без доступа к файлам и процессам игрока.
set(QJS_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/quickjs)
add_library(flovmp-quickjs STATIC
    ${QJS_DIR}/quickjs.c
    ${QJS_DIR}/libregexp.c
    ${QJS_DIR}/libunicode.c
    ${QJS_DIR}/dtoa.c
)
set_target_properties(flovmp-quickjs PROPERTIES C_STANDARD 11 C_STANDARD_REQUIRED ON)
target_include_directories(flovmp-quickjs PUBLIC ${QJS_DIR})
target_compile_definitions(flovmp-quickjs PRIVATE WIN32_LEAN_AND_MEAN _WIN32_WINNT=0x0601 _GNU_SOURCE)
if (MSVC)
    target_compile_options(flovmp-quickjs PRIVATE /experimental:c11atomics /utf-8 /W0)
    set_property(TARGET flovmp-quickjs PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

add_library(flovmp-client SHARED
    src/main.cpp
    src/common.cpp
    src/identity.cpp
    src/net.cpp
    src/game.cpp
    src/ui.cpp
    src/image.cpp
    src/http.cpp
    src/script.cpp
    src/browser.cpp
    src/crash.cpp
    src/voice.cpp
    src/world.cpp
    res/client.rc
    ${IMGUI_DIR}/imgui.cpp
    ${IMGUI_DIR}/imgui_draw.cpp
    ${IMGUI_DIR}/imgui_tables.cpp
    ${IMGUI_DIR}/imgui_widgets.cpp
    ${IMGUI_DIR}/backends/imgui_impl_dx11.cpp
)

target_compile_features(flovmp-client PRIVATE cxx_std_20)
target_include_directories(flovmp-client PRIVATE src ${IMGUI_DIR})
# Версия клиента берётся из общего файла VERSION, чтобы сервер видел,
# какая сборка у игрока, и мог просить обновиться.
set(FLOVMP_VERSION_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../../../VERSION")
if(EXISTS "${FLOVMP_VERSION_FILE}")
    file(READ "${FLOVMP_VERSION_FILE}" FLOVMP_VERSION_RAW)
    string(STRIP "${FLOVMP_VERSION_RAW}" FLOVMP_VERSION)
else()
    set(FLOVMP_VERSION "dev")
endif()
message(STATUS "FloV:MP client version: ${FLOVMP_VERSION}")

target_compile_definitions(flovmp-client PRIVATE FLOVMP_CLIENT_VERSION="${FLOVMP_VERSION}" WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE
    IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS)
if (MSVC)
    target_compile_options(flovmp-client PRIVATE /utf-8 /W3 /EHsc /GS /guard:cf)
    target_link_options(flovmp-client PRIVATE /GUARD:CF /DYNAMICBASE /NXCOMPAT)
    # Статический CRT: у игрока может не быть нужного VC++ Redistributable.
    set_property(TARGET flovmp-client PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()
target_link_libraries(flovmp-client PRIVATE ws2_32 bcrypt crypt32 iphlpapi version d3d11 d3dcompiler shell32 ole32 winmm windowscodecs winhttp flovmp-quickjs)

set_target_properties(flovmp-client PROPERTIES
    OUTPUT_NAME "FloVMP"
    SUFFIX ".asi"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release"
)

# --- хост браузеров flovmp-cef.exe (пункт 26b) --------------------------------
# Chromium (CEF) в отдельном процессе рядом с игрой. Нужен CEF SDK:
# scripts/fetch-cef-sdk.ps1 кладёт его в .work/cef/cef131 и собирает
# libcef_dll_wrapper. Без SDK клиент собирается как раньше, браузеров нет.
set(FLOVMP_CEF_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../../../.work/cef/cef131" CACHE PATH "CEF SDK")
set(FLOVMP_CEF_WRAPPER "${CMAKE_CURRENT_SOURCE_DIR}/../../../.work/cef/build/libcef_dll_wrapper/Release/libcef_dll_wrapper.lib" CACHE FILEPATH "libcef_dll_wrapper.lib")
if (WIN32 AND EXISTS "${FLOVMP_CEF_ROOT}/include/cef_app.h" AND EXISTS "${FLOVMP_CEF_WRAPPER}")
    message(STATUS "FloV:MP: хост браузеров — CEF из ${FLOVMP_CEF_ROOT}")
    add_executable(flovmp-cef WIN32 cef-host/main.cpp cef-host/flovmp-cef.manifest res/client.rc)
    target_compile_features(flovmp-cef PRIVATE cxx_std_20)
    target_include_directories(flovmp-cef PRIVATE "${FLOVMP_CEF_ROOT}" src)
    target_compile_definitions(flovmp-cef PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE
        CEF_USE_SANDBOX PSAPI_VERSION=1)
    if (MSVC)
        target_compile_options(flovmp-cef PRIVATE /utf-8 /W3 /EHsc)
        set_property(TARGET flovmp-cef PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()
    target_link_libraries(flovmp-cef PRIVATE
        "${FLOVMP_CEF_WRAPPER}"
        "${FLOVMP_CEF_ROOT}/Release/libcef.lib"
        "${FLOVMP_CEF_ROOT}/Release/cef_sandbox.lib"
        # CEF 131: зависимости статической Windows sandbox library.
        advapi32 dbghelp delayimp ntdll oleaut32 powrprof propsys psapi
        setupapi shell32 shcore userenv version wbemuuid windowsapp winmm
        # Базовые Windows-зависимости CEF.
        comctl32 gdi32 rpcrt4 shlwapi ws2_32)
    # Символы хоста — для стека в cef-host.log при падении. PDB лежит вне папки
    # cef (в комплект игрока не попадает); путь к нему зашит в exe.
    if (MSVC)
        target_compile_options(flovmp-cef PRIVATE /Zi)
        target_link_options(flovmp-cef PRIVATE /DEBUG /OPT:REF /OPT:ICF)
        set_target_properties(flovmp-cef PROPERTIES PDB_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/pdb")
    endif()
    set(FLOVMP_CEF_OUT "${CMAKE_BINARY_DIR}/bin/Release/cef")
    set_target_properties(flovmp-cef PROPERTIES RUNTIME_OUTPUT_DIRECTORY_RELEASE "${FLOVMP_CEF_OUT}")
    # Рядом с хостом — всё, что нужно Chromium: библиотеки и ресурсы.
    add_custom_command(TARGET flovmp-cef POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${FLOVMP_CEF_ROOT}/Release" "${FLOVMP_CEF_OUT}"
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${FLOVMP_CEF_ROOT}/Resources" "${FLOVMP_CEF_OUT}"
        COMMAND ${CMAKE_COMMAND} -E remove -f "${FLOVMP_CEF_OUT}/libcef.lib" "${FLOVMP_CEF_OUT}/cef_sandbox.lib"
        # DirectX-компилятор WebGPU игроку не нужен — минус ~27 МБ.
        # SwiftShader и vulkan-1.dll НЕ удалять: при --disable-gpu Chromium
        # всё равно поднимает GPU-процесс на программном SwiftShader; без него
        # тот падает при каждом запуске, и после нескольких падений Chromium
        # закрывает весь хост («GPU process isn't usable»).
        COMMAND ${CMAKE_COMMAND} -E remove -f "${FLOVMP_CEF_OUT}/dxcompiler.dll" "${FLOVMP_CEF_OUT}/dxil.dll"
        # Из 55 языков интерфейса Chromium — русский и английский.
        COMMAND ${CMAKE_COMMAND} -DDIR=${FLOVMP_CEF_OUT}/locales -P "${CMAKE_CURRENT_SOURCE_DIR}/cef-host/prune-locales.cmake")
else()
    message(STATUS "FloV:MP: CEF SDK не найден — хост браузеров не собирается")
endif()

# --- средство разработки: предпросмотр интерфейса без GTA V --------------------
# Отдельное окно DirectX 11, в которое ui.cpp рисует настоящий интерфейс
# клиента. В поставку не входит, собирается только локально.
option(FLOVMP_BUILD_UI_PREVIEW "Собрать средство предпросмотра интерфейса" ON)
if (FLOVMP_BUILD_UI_PREVIEW AND WIN32)
    add_executable(flovmp-ui-preview WIN32
        tools/ui-preview/main.cpp
        src/browser.cpp
        res/client.rc
        src/common.cpp
        src/ui.cpp
        src/image.cpp
        src/http.cpp
        ${IMGUI_DIR}/imgui.cpp
        ${IMGUI_DIR}/imgui_draw.cpp
        ${IMGUI_DIR}/imgui_tables.cpp
        ${IMGUI_DIR}/imgui_widgets.cpp
        ${IMGUI_DIR}/backends/imgui_impl_dx11.cpp
    )
    target_compile_features(flovmp-ui-preview PRIVATE cxx_std_20)
    target_include_directories(flovmp-ui-preview PRIVATE src ${IMGUI_DIR})
    target_compile_definitions(flovmp-ui-preview PRIVATE FLOVMP_CLIENT_VERSION="${FLOVMP_VERSION}"
        WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE
        IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS)
    if (MSVC)
        target_compile_options(flovmp-ui-preview PRIVATE /utf-8 /W3 /EHsc)
        set_property(TARGET flovmp-ui-preview PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()
    target_link_libraries(flovmp-ui-preview PRIVATE d3d11 dxgi shell32 ole32 version windowscodecs winhttp bcrypt)
    set_target_properties(flovmp-ui-preview PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
        RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin/Debug"
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release"
    )
endif()

# --- проверка браузеров без GTA V (пункт 26b) ------------------------------------
if (FLOVMP_BUILD_UI_PREVIEW AND WIN32)
    add_executable(flovmp-browser-test
        tools/browser-test/main.cpp
        src/browser.cpp
        src/common.cpp
        res/client.rc
        ${IMGUI_DIR}/imgui.cpp
        ${IMGUI_DIR}/imgui_draw.cpp
        ${IMGUI_DIR}/imgui_tables.cpp
        ${IMGUI_DIR}/imgui_widgets.cpp
    )
    target_compile_features(flovmp-browser-test PRIVATE cxx_std_20)
    target_include_directories(flovmp-browser-test PRIVATE src ${IMGUI_DIR})
    target_compile_definitions(flovmp-browser-test PRIVATE FLOVMP_BROWSER_TEST FLOVMP_CLIENT_VERSION="${FLOVMP_VERSION}"
        WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS)
    if (MSVC)
        target_compile_options(flovmp-browser-test PRIVATE /utf-8 /W3 /EHsc)
        set_property(TARGET flovmp-browser-test PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()
    target_link_libraries(flovmp-browser-test PRIVATE d3d11 dxgi shell32 ole32 version bcrypt ws2_32)
    set_target_properties(flovmp-browser-test PROPERTIES RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release")
endif()

# --- проверка клиентского движка JS без GTA V (пункт 26) -------------------------
if (FLOVMP_BUILD_UI_PREVIEW AND WIN32)
    add_executable(flovmp-script-test
        tools/script-test/main.cpp
        src/script.cpp
        src/browser.cpp
        src/common.cpp
        src/ui.cpp
        src/image.cpp
        src/http.cpp
        res/client.rc
        ${IMGUI_DIR}/imgui.cpp
        ${IMGUI_DIR}/imgui_draw.cpp
        ${IMGUI_DIR}/imgui_tables.cpp
        ${IMGUI_DIR}/imgui_widgets.cpp
        ${IMGUI_DIR}/backends/imgui_impl_dx11.cpp
    )
    target_compile_features(flovmp-script-test PRIVATE cxx_std_20)
    target_include_directories(flovmp-script-test PRIVATE src ${IMGUI_DIR})
    # Пример client_packages из SDK — тест запускает его, чтобы пример не устарел.
    get_filename_component(FLOVMP_SDK_PACKAGES "${CMAKE_CURRENT_SOURCE_DIR}/../../../scripts/package-templates/common/sdk/client_packages" ABSOLUTE)
    target_compile_definitions(flovmp-script-test PRIVATE FLOVMP_SDK_PACKAGES=L"${FLOVMP_SDK_PACKAGES}")
    if (TARGET flovmp-cef)
        target_compile_definitions(flovmp-script-test PRIVATE FLOVMP_CEF_HOST=L"${CMAKE_BINARY_DIR}/bin/Release/cef/flovmp-cef.exe")
    endif()
    target_compile_definitions(flovmp-script-test PRIVATE FLOVMP_BROWSER_TEST FLOVMP_CLIENT_VERSION="${FLOVMP_VERSION}"
        WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS)
    if (MSVC)
        target_compile_options(flovmp-script-test PRIVATE /utf-8 /W3 /EHsc)
        set_property(TARGET flovmp-script-test PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()
    target_link_libraries(flovmp-script-test PRIVATE flovmp-quickjs d3d11 dxgi shell32 ole32 version windowscodecs winhttp bcrypt)
    set_target_properties(flovmp-script-test PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release")
endif()
