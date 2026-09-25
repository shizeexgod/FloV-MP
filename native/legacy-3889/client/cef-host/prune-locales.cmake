# Оставить в папке locales CEF только ru и en-US.
file(GLOB paks "${DIR}/*.pak")
foreach(pak ${paks})
    get_filename_component(name "${pak}" NAME)
    if (NOT name STREQUAL "ru.pak" AND NOT name STREQUAL "en-US.pak")
        file(REMOVE "${pak}")
    endif()
endforeach()
