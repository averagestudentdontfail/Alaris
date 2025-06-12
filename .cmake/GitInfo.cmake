# .cmake/GitInfo.cmake
# Basic git information for build metadata

find_package(Git QUIET)

function(get_git_info)
    set(GIT_COMMIT_HASH "unknown")
    set(GIT_BRANCH "unknown")
    set(BUILD_TIMESTAMP "unknown")
    
    if(GIT_FOUND AND EXISTS "${CMAKE_SOURCE_DIR}/.git")
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-parse --short HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_COMMIT_HASH
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-parse --abbrev-ref HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_BRANCH
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
    endif()
    
    string(TIMESTAMP BUILD_TIMESTAMP "%Y-%m-%d %H:%M:%S UTC" UTC)
    
    set(GIT_COMMIT_HASH "${GIT_COMMIT_HASH}" PARENT_SCOPE)
    set(GIT_BRANCH "${GIT_BRANCH}" PARENT_SCOPE)
    set(BUILD_TIMESTAMP "${BUILD_TIMESTAMP}" PARENT_SCOPE)
endfunction()

function(create_build_info_file output_file)
    get_git_info()
    
    file(WRITE "${output_file}"
        "BUILD_DATE=${BUILD_TIMESTAMP}\n"
        "GIT_COMMIT=${GIT_COMMIT_HASH}\n"
        "GIT_BRANCH=${GIT_BRANCH}\n"
        "BUILD_TYPE=${CMAKE_BUILD_TYPE}\n"
        "VERSION=${PROJECT_VERSION}\n"
    )
endfunction()

get_git_info()