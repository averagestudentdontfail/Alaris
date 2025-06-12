# cmake/modules/GitInfo.cmake
# Extract Git information for build metadata

find_package(Git QUIET)

function(get_git_info)
    set(GIT_COMMIT_HASH "unknown")
    set(GIT_BRANCH "unknown")
    set(GIT_TAG "")
    set(GIT_DIRTY FALSE)
    set(BUILD_TIMESTAMP "unknown")
    
    if(GIT_FOUND AND EXISTS "${CMAKE_SOURCE_DIR}/.git")
        # Get commit hash
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-parse HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_COMMIT_HASH
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Get short commit hash
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-parse --short HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_COMMIT_HASH_SHORT
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Get branch name
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-parse --abbrev-ref HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_BRANCH
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Get tag (if on a tag)
        execute_process(
            COMMAND ${GIT_EXECUTABLE} describe --exact-match --tags HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_TAG
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Check if working directory is dirty
        execute_process(
            COMMAND ${GIT_EXECUTABLE} diff --quiet --exit-code
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            RESULT_VARIABLE GIT_DIFF_RESULT
            ERROR_QUIET
        )
        
        execute_process(
            COMMAND ${GIT_EXECUTABLE} diff --quiet --cached --exit-code
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            RESULT_VARIABLE GIT_CACHED_RESULT
            ERROR_QUIET
        )
        
        if(GIT_DIFF_RESULT OR GIT_CACHED_RESULT)
            set(GIT_DIRTY TRUE)
        endif()
        
        # Get commit date
        execute_process(
            COMMAND ${GIT_EXECUTABLE} log -1 --format=%ci
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_COMMIT_DATE
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Get commit count
        execute_process(
            COMMAND ${GIT_EXECUTABLE} rev-list --count HEAD
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_COMMIT_COUNT
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        # Get last tag and distance
        execute_process(
            COMMAND ${GIT_EXECUTABLE} describe --tags --long --dirty --always
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE GIT_DESCRIBE
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        
        message(STATUS "Git information:")
        message(STATUS "  Commit: ${GIT_COMMIT_HASH_SHORT}")
        message(STATUS "  Branch: ${GIT_BRANCH}")
        if(GIT_TAG)
            message(STATUS "  Tag: ${GIT_TAG}")
        endif()
        if(GIT_DIRTY)
            message(STATUS "  Status: dirty (uncommitted changes)")
        else()
            message(STATUS "  Status: clean")
        endif()
        message(STATUS "  Describe: ${GIT_DESCRIBE}")
    else()
        message(STATUS "Git not found or not a git repository")
    endif()
    
    # Generate build timestamp
    string(TIMESTAMP BUILD_TIMESTAMP "%Y-%m-%d %H:%M:%S UTC" UTC)
    
    # Set variables in parent scope
    set(GIT_COMMIT_HASH "${GIT_COMMIT_HASH}" PARENT_SCOPE)
    set(GIT_COMMIT_HASH_SHORT "${GIT_COMMIT_HASH_SHORT}" PARENT_SCOPE)
    set(GIT_BRANCH "${GIT_BRANCH}" PARENT_SCOPE)
    set(GIT_TAG "${GIT_TAG}" PARENT_SCOPE)
    set(GIT_DIRTY ${GIT_DIRTY} PARENT_SCOPE)
    set(GIT_COMMIT_DATE "${GIT_COMMIT_DATE}" PARENT_SCOPE)
    set(GIT_COMMIT_COUNT "${GIT_COMMIT_COUNT}" PARENT_SCOPE)
    set(GIT_DESCRIBE "${GIT_DESCRIBE}" PARENT_SCOPE)
    set(BUILD_TIMESTAMP "${BUILD_TIMESTAMP}" PARENT_SCOPE)
    
    # Set convenient boolean variables
    if(GIT_FOUND AND EXISTS "${CMAKE_SOURCE_DIR}/.git")
        set(HAS_GIT TRUE PARENT_SCOPE)
    else()
        set(HAS_GIT FALSE PARENT_SCOPE)
    endif()
    
    # Create version string with git info
    if(GIT_TAG AND NOT GIT_DIRTY)
        set(VERSION_STRING "${GIT_TAG}" PARENT_SCOPE)
    elseif(GIT_TAG)
        set(VERSION_STRING "${GIT_TAG}-dirty" PARENT_SCOPE)
    else()
        if(GIT_DIRTY)
            set(VERSION_STRING "${PROJECT_VERSION}-${GIT_COMMIT_HASH_SHORT}-dirty" PARENT_SCOPE)
        else()
            set(VERSION_STRING "${PROJECT_VERSION}-${GIT_COMMIT_HASH_SHORT}" PARENT_SCOPE)
        endif()
    endif()
endfunction()

# Function to create a git pre-commit hook
function(setup_git_hooks)
    if(NOT GIT_FOUND OR NOT EXISTS "${CMAKE_SOURCE_DIR}/.git")
        return()
    endif()
    
    set(HOOKS_DIR "${CMAKE_SOURCE_DIR}/.git/hooks")
    set(PRE_COMMIT_HOOK "${HOOKS_DIR}/pre-commit")
    
    # Create pre-commit hook that runs clang-format
    if(NOT EXISTS "${PRE_COMMIT_HOOK}")
        file(WRITE "${PRE_COMMIT_HOOK}"
            "#!/bin/bash\n"
            "# Alaris pre-commit hook\n"
            "\n"
            "# Check if clang-format is available\n"
            "if ! command -v clang-format &> /dev/null; then\n"
            "    echo \"Warning: clang-format not found, skipping format check\"\n"
            "    exit 0\n"
            "fi\n"
            "\n"
            "# Get list of changed files\n"
            "changed_files=$(git diff --cached --name-only --diff-filter=ACM | grep -E '\\.(cpp|h|hpp)$')\n"
            "\n"
            "if [ -z \"$changed_files\" ]; then\n"
            "    exit 0\n"
            "fi\n"
            "\n"
            "# Check formatting\n"
            "format_issues=false\n"
            "for file in $changed_files; do\n"
            "    if ! clang-format --dry-run --Werror \"$file\" >/dev/null 2>&1; then\n"
            "        echo \"Format issue in $file\"\n"
            "        format_issues=true\n"
            "    fi\n"
            "done\n"
            "\n"
            "if [ \"$format_issues\" = true ]; then\n"
            "    echo \"Format issues found. Run 'clang-format -i <files>' to fix.\"\n"
            "    exit 1\n"
            "fi\n"
            "\n"
            "exit 0\n"
        )
        
        # Make hook executable
        execute_process(
            COMMAND chmod +x "${PRE_COMMIT_HOOK}"
            ERROR_QUIET
        )
        
        message(STATUS "Git pre-commit hook installed")
    endif()
endfunction()

# Function to check if repository is clean
function(require_clean_repository)
    if(NOT GIT_FOUND OR NOT EXISTS "${CMAKE_SOURCE_DIR}/.git")
        return()
    endif()
    
    # Check for uncommitted changes
    execute_process(
        COMMAND ${GIT_EXECUTABLE} diff --quiet --exit-code
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        RESULT_VARIABLE UNCOMMITTED_CHANGES
        ERROR_QUIET
    )
    
    execute_process(
        COMMAND ${GIT_EXECUTABLE} diff --quiet --cached --exit-code
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        RESULT_VARIABLE STAGED_CHANGES
        ERROR_QUIET
    )
    
    if(UNCOMMITTED_CHANGES OR STAGED_CHANGES)
        message(FATAL_ERROR 
            "Repository has uncommitted changes. "
            "Please commit or stash changes before building release.")
    endif()
    
    # Check for untracked files
    execute_process(
        COMMAND ${GIT_EXECUTABLE} ls-files --others --exclude-standard
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        OUTPUT_VARIABLE UNTRACKED_FILES
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    
    if(UNTRACKED_FILES)
        message(WARNING "Repository has untracked files: ${UNTRACKED_FILES}")
    endif()
endfunction()

# Function to create build info file
function(create_build_info_file output_file)
    get_git_info()
    
    file(WRITE "${output_file}"
        "# Alaris Build Information\n"
        "# Generated automatically - do not edit\n"
        "\n"
        "BUILD_DATE=${BUILD_TIMESTAMP}\n"
        "GIT_COMMIT=${GIT_COMMIT_HASH}\n"
        "GIT_BRANCH=${GIT_BRANCH}\n"
        "GIT_TAG=${GIT_TAG}\n"
        "GIT_DIRTY=${GIT_DIRTY}\n"
        "BUILD_TYPE=${CMAKE_BUILD_TYPE}\n"
        "VERSION=${PROJECT_VERSION}\n"
        "VERSION_STRING=${VERSION_STRING}\n"
        "COMPILER=${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}\n"
        "PLATFORM=${CMAKE_SYSTEM_NAME} ${CMAKE_SYSTEM_PROCESSOR}\n"
    )
    
    message(STATUS "Build info written to: ${output_file}")
endfunction()

# Auto-detect git info when module is included
get_git_info()