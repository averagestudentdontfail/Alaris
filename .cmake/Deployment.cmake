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

# Function to install main application targets (executables, libraries)
# This is called from src/quantlib/CMakeLists.txt and other places where targets are defined
# For simplicity, individual install commands are now directly with target definitions.
# This file will focus on installing non-target files and setting up uninstall.

# Install common files and directories
function(install_common_files)
    # Configuration files
    install(DIRECTORY "${CMAKE_SOURCE_DIR}/config/"
            DESTINATION "${ALARIS_INSTALL_CONFIGDIR}"
            COMPONENT Configuration
            FILES_MATCHING PATTERN "*.yaml"
            PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)

    # Scripts (ensure they are executable)
    install(DIRECTORY "${CMAKE_SOURCE_DIR}/scripts/"
            DESTINATION "${ALARIS_INSTALL_BINDIR}" # Scripts often go to bin
            COMPONENT Scripts
            FILES_MATCHING PATTERN "*.sh"
            PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE
                        GROUP_READ GROUP_EXECUTE
                        WORLD_READ WORLD_EXECUTE)

    # Monitoring files
    install(DIRECTORY "${CMAKE_SOURCE_DIR}/monitoring/"
            DESTINATION "${ALARIS_INSTALL_DATADIR}/monitoring"
            COMPONENT Monitoring
            FILES_MATCHING PATTERN "*.yml" PATTERN "*.yaml" PATTERN "*.json"
            PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)

    # Documentation
    install(FILES
            "${CMAKE_SOURCE_DIR}/README.md"
            "${CMAKE_SOURCE_DIR}/docs/DEPLOYMENT.md" # Assuming this exists
            DESTINATION "${ALARIS_INSTALL_DOCDIR}"
            COMPONENT Documentation
            PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ)

    # Development headers (if ALARIS_INSTALL_DEVELOPMENT is ON)
    # Header installation is now handled per-library in their respective CMakeLists.txt
    # if(ALARIS_INSTALL_DEVELOPMENT)
    #     # This is a placeholder; actual header installation should be more specific
    #     # and typically handled alongside library definitions.
    #     message(STATUS "Development file installation enabled (placeholder in Deployment.cmake)")
    # endif()
endfunction()

# System integration (e.g., systemd services, desktop files)
function(setup_system_integration)
    if(UNIX AND NOT APPLE AND EXISTS "${CMAKE_SOURCE_DIR}/cmake/templates/alaris-quantlib.service.in")
        # Systemd service
        set(SERVICE_INSTALL_DIR "lib/systemd/system") # Common path
        # Or use CMAKE_INSTALL_SYSTEMD_SERVICEDIR if defined/available for your CMake version

        configure_file(
            "${CMAKE_SOURCE_DIR}/cmake/templates/alaris-quantlib.service.in"
            "${CMAKE_BINARY_DIR}/alaris-quantlib.service"
            @ONLY
        )
        install(FILES "${CMAKE_BINARY_DIR}/alaris-quantlib.service"
                DESTINATION "${SERVICE_INSTALL_DIR}"
                COMPONENT SystemIntegration
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
                OPTIONAL)
    endif()
    # Add desktop file installation similarly if needed
endfunction()

# Post-install script setup
function(setup_post_install_script)
    if(EXISTS "${CMAKE_SOURCE_DIR}/scripts/post-install.sh") # Assuming post-install.sh does not need @ONLY configure_file
        install(PROGRAMS "${CMAKE_SOURCE_DIR}/scripts/post-install.sh"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT PostInstall
                RENAME post-install-alaris.sh) # Optional rename
    elseif(EXISTS "${CMAKE_SOURCE_DIR}/scripts/post-install.sh.in")
         configure_file(
            "${CMAKE_SOURCE_DIR}/scripts/post-install.sh.in"
            "${CMAKE_BINARY_DIR}/post-install.sh"
            @ONLY
        )
        install(PROGRAMS "${CMAKE_BINARY_DIR}/post-install.sh"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT PostInstall
                RENAME post-install-alaris.sh) # Optional rename
    endif()
endfunction()

# Uninstall target setup
function(setup_cmake_uninstall_target)
    if(EXISTS "${CMAKE_SOURCE_DIR}/.cmake/templates/cmake_uninstall.cmake.in")
        configure_file(
            "${CMAKE_SOURCE_DIR}/.cmake/templates/cmake_uninstall.cmake.in"
            "${CMAKE_CURRENT_BINARY_DIR}/cmake_uninstall.cmake"
            IMMEDIATE @ONLY
        )
        add_custom_target(uninstall
            COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_BINARY_DIR}/cmake_uninstall.cmake"
            COMMENT "Uninstalling ${PROJECT_NAME} from ${CMAKE_INSTALL_PREFIX}"
        )
    else()
        message(WARNING "${CMAKE_SOURCE_DIR}/.cmake/templates/cmake_uninstall.cmake.in not found. Uninstall target will not be created.")
    endif()
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

# Export an installation an AlarisConfig.cmake file so other projects can find it
# This part is more advanced and depends on how you want your project to be findable
# include(CMakePackageConfigHelpers)
# configure_package_config_file(...)
# install(EXPORT AlarisTargets ... )
# install(FILES "${PROJECT_BINARY_DIR}/AlarisConfig.cmake" DESTINATION "lib/cmake/Alaris")