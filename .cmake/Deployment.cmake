# .cmake/Deployment.cmake
# Deployment configuration for Alaris
include(GNUInstallDirs) # Defines CMAKE_INSTALL_BINDIR, LIBDIR, INCLUDEDIR, etc.

# Define standard installation directories relative to CMAKE_INSTALL_PREFIX
set(ALARIS_INSTALL_BINDIR ${CMAKE_INSTALL_BINDIR})
set(ALARIS_INSTALL_LIBDIR ${CMAKE_INSTALL_LIBDIR})
set(ALARIS_INSTALL_INCLUDEDIR "${CMAKE_INSTALL_INCLUDEDIR}/${PROJECT_NAME}") # e.g. include/Alaris
set(ALARIS_INSTALL_CONFIGDIR "${CMAKE_INSTALL_SYSCONFDIR}/${PROJECT_NAME}") # e.g. etc/Alaris
set(ALARIS_INSTALL_DATADIR "${CMAKE_INSTALL_DATADIR}/${PROJECT_NAME}")     # e.g. share/Alaris
set(ALARIS_INSTALL_DOCDIR "${CMAKE_INSTALL_DOCDIR}/${PROJECT_NAME}")       # e.g. share/doc/Alaris

# Install common files and directories
function(install_common_files)
    # Configuration files
    if(EXISTS "${CMAKE_SOURCE_DIR}/config")
        install(DIRECTORY "${CMAKE_SOURCE_DIR}/config/"
                DESTINATION "${ALARIS_INSTALL_CONFIGDIR}"
                COMPONENT Configuration
                FILES_MATCHING PATTERN "*.yaml" PATTERN "*.yml"
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)
    endif()

    # Scripts (ensure they are executable)
    if(EXISTS "${CMAKE_SOURCE_DIR}/scripts")
        install(DIRECTORY "${CMAKE_SOURCE_DIR}/scripts/"
                DESTINATION "${ALARIS_INSTALL_BINDIR}/scripts"
                COMPONENT Scripts
                FILES_MATCHING PATTERN "*.sh"
                PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE
                            GROUP_READ GROUP_EXECUTE
                            WORLD_READ WORLD_EXECUTE)
    endif()

    # Monitoring files
    if(EXISTS "${CMAKE_SOURCE_DIR}/monitoring")
        install(DIRECTORY "${CMAKE_SOURCE_DIR}/monitoring/"
                DESTINATION "${ALARIS_INSTALL_DATADIR}/monitoring"
                COMPONENT Monitoring
                FILES_MATCHING PATTERN "*.yml" PATTERN "*.yaml" PATTERN "*.json"
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)
    endif()

    # Documentation
    if(EXISTS "${CMAKE_SOURCE_DIR}/README.md")
        install(FILES "${CMAKE_SOURCE_DIR}/README.md"
                DESTINATION "${ALARIS_INSTALL_DOCDIR}"
                COMPONENT Documentation
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)
    endif()
    
    if(EXISTS "${CMAKE_SOURCE_DIR}/docs/DEPLOYMENT.md")
        install(FILES "${CMAKE_SOURCE_DIR}/docs/DEPLOYMENT.md"
                DESTINATION "${ALARIS_INSTALL_DOCDIR}"
                COMPONENT Documentation
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)
    endif()
endfunction()

# System integration (e.g., systemd services, desktop files)
function(setup_system_integration)
    if(UNIX AND NOT APPLE)
        # Create a simple systemd service template
        set(SERVICE_CONTENT 
"[Unit]
Description=Alaris Trading System
After=network.target

[Service]
Type=simple
ExecStart=${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris
Restart=always
User=alaris
Group=alaris

[Install]
WantedBy=multi-user.target")

        file(WRITE "${CMAKE_BINARY_DIR}/alaris.service" "${SERVICE_CONTENT}")
        
        install(FILES "${CMAKE_BINARY_DIR}/alaris.service"
                DESTINATION "lib/systemd/system"
                COMPONENT SystemIntegration
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
                OPTIONAL)
    endif()
endfunction()

# Post-install script setup
function(setup_post_install_script)
    if(EXISTS "${CMAKE_SOURCE_DIR}/scripts/post-install.sh")
        install(PROGRAMS "${CMAKE_SOURCE_DIR}/scripts/post-install.sh"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT PostInstall
                RENAME post-install-alaris.sh)
    endif()
endfunction()

# Create a simple uninstall target without requiring template
function(setup_cmake_uninstall_target)
    # Create uninstall script content directly
    set(UNINSTALL_SCRIPT_CONTENT 
"#!/usr/bin/env cmake -P
# Alaris uninstall script

if(NOT DEFINED CMAKE_INSTALL_MANIFEST)
    set(CMAKE_INSTALL_MANIFEST \"install_manifest.txt\")
endif()

if(NOT EXISTS \"\${CMAKE_INSTALL_MANIFEST}\")
    message(FATAL_ERROR \"Cannot find install manifest: \${CMAKE_INSTALL_MANIFEST}\")
endif()

file(READ \"\${CMAKE_INSTALL_MANIFEST}\" files)
string(REGEX REPLACE \"\\n\" \";\" files \"\${files}\")

foreach(file \${files})
    message(STATUS \"Uninstalling: \$ENV{DESTDIR}\${file}\")
    if(IS_SYMLINK \"\$ENV{DESTDIR}\${file}\" OR EXISTS \"\$ENV{DESTDIR}\${file}\")
        exec_program(
            \"${CMAKE_COMMAND}\" ARGS \"-E remove \\\"\$ENV{DESTDIR}\${file}\\\"\"
            OUTPUT_VARIABLE rm_out
            RETURN_VALUE rm_retval
        )
        if(NOT \"\${rm_retval}\" STREQUAL 0)
            message(FATAL_ERROR \"Problem when removing: \$ENV{DESTDIR}\${file}\")
        endif()
    else()
        message(STATUS \"File does not exist: \$ENV{DESTDIR}\${file}\")
    endif()
endforeach()
")

    file(WRITE "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake" "${UNINSTALL_SCRIPT_CONTENT}")
    
    add_custom_target(uninstall
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake"
        COMMENT "Uninstalling ${PROJECT_NAME} from ${CMAKE_INSTALL_PREFIX}"
    )
    
    message(STATUS "Uninstall target created")
endfunction()

# Call the functions to set up installations
install_common_files()
setup_system_integration()
setup_post_install_script()
setup_cmake_uninstall_target()

# Print installation summary (GNUInstallDirs paths are relative to CMAKE_INSTALL_PREFIX)
message(STATUS "Installation Summary (relative to CMAKE_INSTALL_PREFIX='${CMAKE_INSTALL_PREFIX}'):")
message(STATUS "  Binaries:      ${ALARIS_INSTALL_BINDIR}")
message(STATUS "  Libraries:     ${ALARIS_INSTALL_LIBDIR}")
message(STATUS "  Headers:       ${ALARIS_INSTALL_INCLUDEDIR}")
message(STATUS "  Config:        ${ALARIS_INSTALL_CONFIGDIR}")
message(STATUS "  Data:          ${ALARIS_INSTALL_DATADIR}")
message(STATUS "  Documentation: ${ALARIS_INSTALL_DOCDIR}")