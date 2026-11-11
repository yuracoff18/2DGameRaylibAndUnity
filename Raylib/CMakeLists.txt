cmake_minimum_required(VERSION 3.28)
project(io LANGUAGES C CXX)

# Generate compile_commands.json
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Dependencies
set(RAYLIB_VERSION 6.0)
find_package(raylib ${RAYLIB_VERSION} QUIET) # QUIET or REQUIRED
if (NOT raylib_FOUND) # If there's none, fetch and build raylib
  include(FetchContent)
  FetchContent_Declare(
    raylib
    DOWNLOAD_EXTRACT_TIMESTAMP OFF
    URL https://github.com/raysan5/raylib/archive/refs/tags/${RAYLIB_VERSION}.tar.gz
  )
  set(BUILD_EXAMPLES OFF CACHE BOOL "Build raylib examples")
  FetchContent_MakeAvailable(raylib)
  # Activar JPG conservando los demas valores de config.h de raylib.
  target_compile_definitions(raylib PRIVATE SUPPORT_FILEFORMAT_JPG=1)
endif()

# Our Project

# Detectar automaticamente las clases de src, incluidas sus subcarpetas.
file(GLOB_RECURSE PROJECT_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.c"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cc"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cxx"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.h"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.hh"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.hpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.hxx"
)

add_executable(${PROJECT_NAME} main.cpp ${PROJECT_SOURCES})
target_include_directories(${PROJECT_NAME} PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
target_compile_features(${PROJECT_NAME} PRIVATE cxx_std_17)
target_link_libraries(${PROJECT_NAME} PRIVATE raylib)

# Mantener los recursos junto al ejecutable, incluso cuando solo cambian assets.
add_custom_target(${PROJECT_NAME}_assets
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/assets"
        "$<TARGET_FILE_DIR:${PROJECT_NAME}>/assets"
    COMMENT "Copiando assets junto al ejecutable"
    VERBATIM
)
add_dependencies(${PROJECT_NAME} ${PROJECT_NAME}_assets)

# Web Configurations
if (PLATFORM STREQUAL "Web")
    set_target_properties(${PROJECT_NAME} PROPERTIES SUFFIX ".html") # Tell Emscripten to build an example.html file.
    set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -s USE_GLFW=3 -s ASSERTIONS=1 -s WASM=1 -s ASYNCIFY")
endif()

# Checks if OSX and links appropriate frameworks (Only required on MacOS)
if (APPLE)
    target_link_libraries(${PROJECT_NAME} PRIVATE "-framework IOKit")
    target_link_libraries(${PROJECT_NAME} PRIVATE "-framework Cocoa")
    target_link_libraries(${PROJECT_NAME} PRIVATE "-framework OpenGL")
    target_link_libraries(${PROJECT_NAME} PRIVATE "-framework QuartzCore")
endif()
