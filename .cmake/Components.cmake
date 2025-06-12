# .cmake/Components.cmake
# Component definitions for Alaris

# Source files for core library
set(QUANTLIB_CORE_SOURCES
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory_manager.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.cpp
)

function(configure_all_components)
    # Core library
    add_library(quantlib STATIC ${QUANTLIB_CORE_SOURCES})
    target_include_directories(quantlib PUBLIC
        $<BUILD_INTERFACE:${CMAKE_SOURCE_DIR}/src>
    )
    target_link_libraries(quantlib PUBLIC
        ${QUANTLIB_TARGET}
        ${YAML_CPP_TARGET}
        Threads::Threads
    )

    # Main executables
    add_executable(quantlib-process ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_link_libraries(quantlib-process PRIVATE quantlib)

    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_link_libraries(alaris PRIVATE quantlib)
    
    # Tool executables
    add_executable(alaris-config ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config.cpp)
    target_link_libraries(alaris-config PRIVATE quantlib)

    add_executable(alaris-system ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system.cpp)
    target_link_libraries(alaris-system PRIVATE quantlib)
    
    # Add automated setup as post-build step for main targets
    add_automated_setup_commands()
    
    # Create a combined target for all components
    add_custom_target(alaris-all
        DEPENDS quantlib-process alaris alaris-config alaris-system
        COMMENT "Building all Alaris components"
    )
endfunction()

# Function to add automated setup commands
function(add_automated_setup_commands)
    # Add post-build commands for automated setup
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        
        # Add post-build command to alaris target (primary executable)
        if(ALARIS_AUTO_SETUP_SCRIPT AND EXISTS "${ALARIS_AUTO_SETUP_SCRIPT}")
            add_custom_command(TARGET alaris POST_BUILD
                COMMAND bash "${ALARIS_AUTO_SETUP_SCRIPT}"
                COMMENT "Running Alaris automated setup..."
                VERBATIM
            )
            message(STATUS "Automated setup will run after building 'alaris' target")
        endif()
        
        # Also add to quantlib-process target
        if(ALARIS_AUTO_SETUP_SCRIPT AND EXISTS "${ALARIS_AUTO_SETUP_SCRIPT}")
            add_custom_command(TARGET quantlib-process POST_BUILD
                COMMAND bash "${ALARIS_AUTO_SETUP_SCRIPT}"
                COMMENT "Running Alaris automated setup..."
                VERBATIM
            )
        endif()
        
        # Add a separate target for manual capability setting
        if(ALARIS_CAPABILITY_SCRIPT AND EXISTS "${ALARIS_CAPABILITY_SCRIPT}")
            add_custom_target(set-capabilities
                COMMAND bash "${ALARIS_CAPABILITY_SCRIPT}"
                COMMENT "Setting Linux capabilities for Alaris executables"
                VERBATIM
                DEPENDS alaris quantlib-process
            )
            message(STATUS "Added 'set-capabilities' target for manual execution")
        endif()
        
    else()
        message(STATUS "Automated setup disabled - capabilities not available")
        
        # Add informational target when capabilities aren't available
        add_custom_target(setup-info
            COMMAND ${CMAKE_COMMAND} -E echo "Note: Linux capabilities not available on this system"
            COMMAND ${CMAKE_COMMAND} -E echo "Alaris will run with standard permissions"
            COMMENT "System setup information"
        )
    endif()
    
    # Always add a manual setup target
    if(ALARIS_AUTO_SETUP_SCRIPT AND EXISTS "${ALARIS_AUTO_SETUP_SCRIPT}")
        add_custom_target(alaris-setup
            COMMAND bash "${ALARIS_AUTO_SETUP_SCRIPT}"
            COMMENT "Running Alaris setup manually"
            VERBATIM
            DEPENDS alaris quantlib-process
        )
        message(STATUS "Added 'alaris-setup' target for manual execution")
    endif()
endfunction()

# Function to create build verification target
function(add_build_verification)
    # Create a target that verifies the build completed successfully
    add_custom_target(verify-build
        COMMAND ${CMAKE_COMMAND} -E echo "=== Alaris Build Verification ==="
        COMMAND ${CMAKE_COMMAND} -E echo "Checking executables..."
        COMMAND test -f "${CMAKE_BINARY_DIR}/bin/quantlib-process" || (echo "ERROR: quantlib-process not found" && exit 1)
        COMMAND test -f "${CMAKE_BINARY_DIR}/bin/alaris" || (echo "ERROR: alaris not found" && exit 1)
        COMMAND ${CMAKE_COMMAND} -E echo "✓ Core executables found"
        COMMAND ${CMAKE_COMMAND} -E echo "Checking scripts..."
        COMMAND test -f "${CMAKE_BINARY_DIR}/start-alaris.sh" || (echo "ERROR: start-alaris.sh not found" && exit 1)
        COMMAND ${CMAKE_COMMAND} -E echo "✓ Startup script found"
        COMMAND ${CMAKE_COMMAND} -E echo "=== Build Verification Complete ==="
        COMMENT "Verifying Alaris build completion"
        DEPENDS alaris quantlib-process alaris-config alaris-system
    )
endfunction()

# Add build verification
add_build_verification()