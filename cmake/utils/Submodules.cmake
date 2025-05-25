# cmake/utils/Submodules.cmake
# Git submodule management for Alaris

function(check_submodules)
    message(STATUS "Checking Git submodules...")

    # Find git executable
    find_package(Git QUIET)
    if(NOT GIT_FOUND)
        message(WARNING "Git not found. Cannot check submodules automatically.")
        return()
    endif()

    # List of required submodules
    set(REQUIRED_SUBMODULES
        "external/quant"
        "external/yaml-cpp"
        "external/lean"
    )

    # List of optional submodules
    set(OPTIONAL_SUBMODULES
        # No optional submodules currently
    )

    set(MISSING_REQUIRED FALSE)
    set(MISSING_OPTIONAL FALSE)

    # Check required submodules
    foreach(submodule ${REQUIRED_SUBMODULES})
        check_submodule_status(${submodule} is_missing is_empty)
        
        if(is_missing OR is_empty)
            message(STATUS "Required submodule missing or empty: ${submodule}")
            set(MISSING_REQUIRED TRUE)
        else()
            message(STATUS "✓ Required submodule OK: ${submodule}")
        endif()
    endforeach()

    # Check optional submodules
    foreach(submodule ${OPTIONAL_SUBMODULES})
        check_submodule_status(${submodule} is_missing is_empty)
        
        if(is_missing OR is_empty)
            message(STATUS "Optional submodule missing: ${submodule}")
            set(MISSING_OPTIONAL TRUE)
        else()
            message(STATUS "✓ Optional submodule OK: ${submodule}")
        endif()
    endforeach()

    # Handle missing submodules
    if(MISSING_REQUIRED)
        message(STATUS "Attempting to initialize required submodules...")
        initialize_submodules("${REQUIRED_SUBMODULES}")
        
        # Re-check after initialization
        foreach(submodule ${REQUIRED_SUBMODULES})
            check_submodule_status(${submodule} is_missing is_empty)
            if(is_missing OR is_empty)
                message(FATAL_ERROR 
                    "Failed to initialize required submodule: ${submodule}\n"
                    "Please run manually:\n"
                    "  git submodule update --init --recursive ${submodule}"
                )
            endif()
        endforeach()
    endif()

    if(MISSING_OPTIONAL)
        message(STATUS 
            "Some optional submodules are missing. To initialize them, run:\n"
            "  git submodule update --init --recursive"
        )
    endif()
endfunction()

function(check_submodule_status submodule_path is_missing_var is_empty_var)
    set(${is_missing_var} FALSE PARENT_SCOPE)
    set(${is_empty_var} FALSE PARENT_SCOPE)

    # Check if directory exists
    if(NOT EXISTS "${CMAKE_SOURCE_DIR}/${submodule_path}")
        set(${is_missing_var} TRUE PARENT_SCOPE)
        return()
    endif()

    # Check if directory is empty
    file(GLOB submodule_contents "${CMAKE_SOURCE_DIR}/${submodule_path}/*")
    if(NOT submodule_contents)
        set(${is_empty_var} TRUE PARENT_SCOPE)
        return()
    endif()

    # For specific submodules, check for key files
    if(submodule_path STREQUAL "external/quant")
        if(NOT EXISTS "${CMAKE_SOURCE_DIR}/${submodule_path}/CMakeLists.txt")
            set(${is_empty_var} TRUE PARENT_SCOPE)
        endif()
    elseif(submodule_path STREQUAL "external/yaml-cpp")
        if(NOT EXISTS "${CMAKE_SOURCE_DIR}/${submodule_path}/CMakeLists.txt")
            set(${is_empty_var} TRUE PARENT_SCOPE)
        endif()
    elseif(submodule_path STREQUAL "external/lean")
        if(NOT EXISTS "${CMAKE_SOURCE_DIR}/${submodule_path}/Lean.sln")
            set(${is_empty_var} TRUE PARENT_SCOPE)
        endif()
    endif()
endfunction()

function(initialize_submodules submodule_list)
    if(NOT GIT_FOUND)
        message(FATAL_ERROR "Git is required to initialize submodules")
    endif()

    foreach(submodule ${submodule_list})
        message(STATUS "Initializing submodule: ${submodule}")
        
        execute_process(
            COMMAND ${GIT_EXECUTABLE} submodule update --init --recursive ${submodule}
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            RESULT_VARIABLE git_result
            OUTPUT_VARIABLE git_output
            ERROR_VARIABLE git_error
        )

        if(NOT git_result EQUAL 0)
            message(WARNING 
                "Failed to initialize submodule ${submodule}:\n"
                "Exit code: ${git_result}\n"
                "Output: ${git_output}\n"
                "Error: ${git_error}"
            )
        else()
            message(STATUS "Successfully initialized: ${submodule}")
        endif()
    endforeach()
endfunction()

function(update_submodules)
    if(NOT GIT_FOUND)
        message(WARNING "Git not found. Cannot update submodules.")
        return()
    endif()

    message(STATUS "Updating all submodules...")
    
    execute_process(
        COMMAND ${GIT_EXECUTABLE} submodule update --recursive --remote
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        RESULT_VARIABLE git_result
        OUTPUT_VARIABLE git_output
        ERROR_VARIABLE git_error
    )

    if(git_result EQUAL 0)
        message(STATUS "Submodules updated successfully")
    else()
        message(WARNING 
            "Failed to update submodules:\n"
            "Exit code: ${git_result}\n"
            "Output: ${git_output}\n"
            "Error: ${git_error}"
        )
    endif()
endfunction()

function(get_submodule_info submodule_path)
    if(NOT GIT_FOUND)
        return()
    endif()

    execute_process(
        COMMAND ${GIT_EXECUTABLE} submodule status ${submodule_path}
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        RESULT_VARIABLE git_result
        OUTPUT_VARIABLE git_output
        ERROR_QUIET
    )

    if(git_result EQUAL 0 AND git_output)
        string(SUBSTRING "${git_output}" 0 1 status_char)
        string(REGEX MATCH "[a-f0-9]+" commit_hash "${git_output}")
        
        if(status_char STREQUAL " ")
            set(status "up-to-date")
        elseif(status_char STREQUAL "+")
            set(status "newer than expected")
        elseif(status_char STREQUAL "-")
            set(status "not initialized")
        elseif(status_char STREQUAL "U")
            set(status "merge conflicts")
        else()
            set(status "unknown")
        endif()

        message(STATUS "Submodule ${submodule_path}: ${status} (${commit_hash})")
    endif()
endfunction()

# Add custom targets for submodule management
function(add_submodule_targets)
    if(NOT GIT_FOUND)
        return()
    endif()

    add_custom_target(submodules-update
        COMMAND ${GIT_EXECUTABLE} submodule update --recursive --remote
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        COMMENT "Updating all submodules"
    )

    add_custom_target(submodules-status
        COMMAND ${GIT_EXECUTABLE} submodule status --recursive
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        COMMENT "Checking submodule status"
    )

    add_custom_target(submodules-clean
        COMMAND ${GIT_EXECUTABLE} submodule foreach --recursive "git clean -xfd"
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        COMMENT "Cleaning all submodules"
    )
endfunction()