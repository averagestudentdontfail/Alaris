# cmake/Install.cmake
# Installation configuration for Alaris

include(GNUInstallDirs)

# Set installation destinations
set(ALARIS_INSTALL_BINDIR ${CMAKE_INSTALL_BINDIR})
set(ALARIS_INSTALL_LIBDIR ${CMAKE_INSTALL_LIBDIR})
set(ALARIS_INSTALL_INCLUDEDIR ${CMAKE_INSTALL_INCLUDEDIR}/alaris)
set(ALARIS_INSTALL_CONFIGDIR ${CMAKE_INSTALL_SYSCONFDIR}/alaris)
set(ALARIS_INSTALL_DATADIR ${CMAKE_INSTALL_DATADIR}/alaris)
set(ALARIS_INSTALL_DOCDIR ${CMAKE_INSTALL_DOCDIR})

# Install main executable
if(TARGET quantlib_process)
    install(TARGETS quantlib_process
        RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR}
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE 
                   GROUP_READ GROUP_EXECUTE 
                   WORLD_READ WORLD_EXECUTE
        COMPONENT Runtime
    )
endif()

# Install libraries (if any are made SHARED in the future)
if(TARGET alaris_core)
    get_target_property(ALARIS_CORE_TYPE alaris_core TYPE)
    if(ALARIS_CORE_TYPE STREQUAL "SHARED_LIBRARY")
        install(TARGETS alaris_core
            LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR}
            RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR}
            COMPONENT Runtime
        )
    endif()
endif()

# Install configuration files
install(DIRECTORY ${CMAKE_SOURCE_DIR}/config/
    DESTINATION ${ALARIS_INSTALL_CONFIGDIR}
    FILES_MATCHING PATTERN "*.yaml"
    PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
    COMPONENT Configuration
)

# Install scripts
install(DIRECTORY ${CMAKE_SOURCE_DIR}/scripts/
    DESTINATION ${ALARIS_INSTALL_BINDIR}
    FILES_MATCHING PATTERN "*.sh"
    PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE 
               GROUP_READ GROUP_EXECUTE 
               WORLD_READ WORLD_EXECUTE
    COMPONENT Scripts
)

# Install monitoring configuration
install(DIRECTORY ${CMAKE_SOURCE_DIR}/monitoring/
    DESTINATION ${ALARIS_INSTALL_DATADIR}/monitoring
    FILES_MATCHING 
        PATTERN "*.yml" 
        PATTERN "*.yaml"
        PATTERN "*.json"
    PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
    COMPONENT Monitoring
)

# Install documentation
install(FILES
    ${CMAKE_SOURCE_DIR}/README.md
    ${CMAKE_SOURCE_DIR}/docs/DEPLOYMENT.md
    DESTINATION ${ALARIS_INSTALL_DOCDIR}
    PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
    COMPONENT Documentation
)

# Install headers (for development installations)
if(CMAKE_BUILD_TYPE STREQUAL "Debug" OR ALARIS_INSTALL_DEVELOPMENT)
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/src/quantlib/
        DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}
        FILES_MATCHING PATTERN "*.h" PATTERN "*.hpp"
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
        COMPONENT Development
    )
endif()

# Create systemd service files on Linux
if(UNIX AND NOT APPLE)
    configure_file(
        ${CMAKE_SOURCE_DIR}/deployment/systemd/alaris-quantlib.service.in
        ${CMAKE_BINARY_DIR}/systemd/alaris-quantlib.service
        @ONLY
    )
    
    install(FILES ${CMAKE_BINARY_DIR}/systemd/alaris-quantlib.service
        DESTINATION ${CMAKE_INSTALL_PREFIX}/lib/systemd/system
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
        COMPONENT SystemIntegration
        OPTIONAL
    )
endif()

# Create desktop entry for GUI tools (if any)
if(UNIX AND NOT APPLE)
    configure_file(
        ${CMAKE_SOURCE_DIR}/deployment/desktop/alaris.desktop.in
        ${CMAKE_BINARY_DIR}/desktop/alaris.desktop
        @ONLY
    )
    
    install(FILES ${CMAKE_BINARY_DIR}/desktop/alaris.desktop
        DESTINATION ${CMAKE_INSTALL_DATADIR}/applications
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
        COMPONENT Desktop
        OPTIONAL
    )
endif()

# Install QuantLib and yaml-cpp if built statically
# Note: This is complex and should be handled carefully to avoid conflicts

# Post-install setup script
configure_file(
    ${CMAKE_SOURCE_DIR}/scripts/post-install.sh.in
    ${CMAKE_BINARY_DIR}/post-install.sh
    @ONLY
)

install(PROGRAMS ${CMAKE_BINARY_DIR}/post-install.sh
    DESTINATION ${ALARIS_INSTALL_BINDIR}
    COMPONENT PostInstall
)

# Define installation components
set(CPACK_COMPONENTS_ALL
    Runtime
    Configuration
    Scripts
    Monitoring
    Documentation
    Development
    SystemIntegration
    Desktop
    PostInstall
)

# Component descriptions
set(CPACK_COMPONENT_RUNTIME_DISPLAY_NAME "Alaris Runtime")
set(CPACK_COMPONENT_RUNTIME_DESCRIPTION "Main Alaris trading system executables")
set(CPACK_COMPONENT_RUNTIME_REQUIRED TRUE)

set(CPACK_COMPONENT_CONFIGURATION_DISPLAY_NAME "Configuration Files")
set(CPACK_COMPONENT_CONFIGURATION_DESCRIPTION "Default configuration files")
set(CPACK_COMPONENT_CONFIGURATION_REQUIRED TRUE)

set(CPACK_COMPONENT_SCRIPTS_DISPLAY_NAME "Management Scripts")
set(CPACK_COMPONENT_SCRIPTS_DESCRIPTION "Build, deployment, and management scripts")

set(CPACK_COMPONENT_MONITORING_DISPLAY_NAME "Monitoring Setup")
set(CPACK_COMPONENT_MONITORING_DESCRIPTION "Prometheus and Grafana configuration")

set(CPACK_COMPONENT_DOCUMENTATION_DISPLAY_NAME "Documentation")
set(CPACK_COMPONENT_DOCUMENTATION_DESCRIPTION "User and deployment documentation")

set(CPACK_COMPONENT_DEVELOPMENT_DISPLAY_NAME "Development Files")
set(CPACK_COMPONENT_DEVELOPMENT_DESCRIPTION "Header files for development")

set(CPACK_COMPONENT_SYSTEMINTEGRATION_DISPLAY_NAME "System Integration")
set(CPACK_COMPONENT_SYSTEMINTEGRATION_DESCRIPTION "Systemd service files")

set(CPACK_COMPONENT_DESKTOP_DISPLAY_NAME "Desktop Integration")
set(CPACK_COMPONENT_DESKTOP_DESCRIPTION "Desktop menu entries")

set(CPACK_COMPONENT_POSTINSTALL_DISPLAY_NAME "Post-Install Setup")
set(CPACK_COMPONENT_POSTINSTALL_DESCRIPTION "Post-installation configuration script")

# Component dependencies
set(CPACK_COMPONENT_CONFIGURATION_DEPENDS Runtime)
set(CPACK_COMPONENT_SCRIPTS_DEPENDS Runtime)
set(CPACK_COMPONENT_MONITORING_DEPENDS Configuration)
set(CPACK_COMPONENT_DEVELOPMENT_DEPENDS Runtime)

# Uninstall target
if(NOT TARGET uninstall)
    configure_file(
        ${CMAKE_SOURCE_DIR}/cmake/templates/cmake_uninstall.cmake.in
        ${CMAKE_BINARY_DIR}/cmake_uninstall.cmake
        IMMEDIATE @ONLY
    )

    add_custom_target(uninstall
        COMMAND ${CMAKE_COMMAND} -P ${CMAKE_BINARY_DIR}/cmake_uninstall.cmake
        COMMENT "Uninstalling Alaris"
    )
endif()

# Print installation summary
function(print_install_summary)
    message(STATUS "Installation configuration:")
    message(STATUS "  Prefix:        ${CMAKE_INSTALL_PREFIX}")
    message(STATUS "  Binaries:      ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_BINDIR}")
    message(STATUS "  Libraries:     ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_LIBDIR}")
    message(STATUS "  Headers:       ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_INCLUDEDIR}")
    message(STATUS "  Configuration: ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}")
    message(STATUS "  Data:          ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_DATADIR}")
    message(STATUS "  Documentation: ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_DOCDIR}")
endfunction()