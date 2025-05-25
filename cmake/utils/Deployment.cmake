# Deployment configuration for Alaris
include(GNUInstallDirs)

# Installation paths
set(ALARIS_INSTALL_PATHS
    BINDIR     ${CMAKE_INSTALL_BINDIR}
    LIBDIR     ${CMAKE_INSTALL_LIBDIR}
    INCLUDEDIR ${CMAKE_INSTALL_INCLUDEDIR}/alaris
    CONFIGDIR  ${CMAKE_INSTALL_SYSCONFDIR}/alaris
    DATADIR    ${CMAKE_INSTALL_DATADIR}/alaris
    DOCDIR     ${CMAKE_INSTALL_DOCDIR}
)

# Installation components
set(ALARIS_COMPONENTS
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

# Component configuration
foreach(COMPONENT ${ALARIS_COMPONENTS})
    string(TOUPPER ${COMPONENT} COMPONENT_UPPER)
    set(CPACK_COMPONENT_${COMPONENT_UPPER}_DISPLAY_NAME "${COMPONENT}")
    set(CPACK_COMPONENT_${COMPONENT_UPPER}_DESCRIPTION "${COMPONENT} files")
    if(COMPONENT STREQUAL "Runtime" OR COMPONENT STREQUAL "Configuration")
        set(CPACK_COMPONENT_${COMPONENT_UPPER}_REQUIRED TRUE)
    endif()
endforeach()

# Install targets
function(install_alaris_targets)
    if(TARGET quantlib_process)
        install(TARGETS quantlib_process
            RUNTIME DESTINATION ${ALARIS_INSTALL_PATHS_BINDIR}
            PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE 
                       GROUP_READ GROUP_EXECUTE 
                       WORLD_READ WORLD_EXECUTE
            COMPONENT Runtime
        )
    endif()

    if(TARGET alaris_core)
        get_target_property(ALARIS_CORE_TYPE alaris_core TYPE)
        if(ALARIS_CORE_TYPE STREQUAL "SHARED_LIBRARY")
            install(TARGETS alaris_core
                LIBRARY DESTINATION ${ALARIS_INSTALL_PATHS_LIBDIR}
                RUNTIME DESTINATION ${ALARIS_INSTALL_PATHS_BINDIR}
                COMPONENT Runtime
            )
        endif()
    endif()
endfunction()

# Install files and directories
function(install_alaris_files)
    # Configuration files
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/config/
        COMPONENT Configuration
        DESTINATION ${ALARIS_INSTALL_PATHS_CONFIGDIR}
        FILES_MATCHING PATTERN "*.yaml"
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
    )

    # Scripts
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/scripts/
        COMPONENT Scripts
        DESTINATION ${ALARIS_INSTALL_PATHS_BINDIR}
        FILES_MATCHING PATTERN "*.sh"
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE 
                   GROUP_READ GROUP_EXECUTE 
                   WORLD_READ WORLD_EXECUTE
    )

    # Monitoring
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/monitoring/
        COMPONENT Monitoring
        DESTINATION ${ALARIS_INSTALL_PATHS_DATADIR}/monitoring
        FILES_MATCHING 
            PATTERN "*.yml" 
            PATTERN "*.yaml"
            PATTERN "*.json"
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
    )

    # Documentation
    install(FILES
        ${CMAKE_SOURCE_DIR}/README.md
        ${CMAKE_SOURCE_DIR}/docs/DEPLOYMENT.md
        DESTINATION ${ALARIS_INSTALL_PATHS_DOCDIR}
        PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
        COMPONENT Documentation
    )

    # Development files
    if(CMAKE_BUILD_TYPE STREQUAL "Debug" OR ALARIS_INSTALL_DEVELOPMENT)
        install(DIRECTORY ${CMAKE_SOURCE_DIR}/src/quantlib/
            COMPONENT Development
            DESTINATION ${ALARIS_INSTALL_PATHS_INCLUDEDIR}
            FILES_MATCHING PATTERN "*.h" PATTERN "*.hpp"
            PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
        )
    endif()
endfunction()

# System integration
function(install_system_integration)
    if(UNIX AND NOT APPLE)
        # Systemd service
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

        # Desktop entry
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
endfunction()

# Post-install setup
function(setup_post_install)
    configure_file(
        ${CMAKE_SOURCE_DIR}/scripts/post-install.sh.in
        ${CMAKE_BINARY_DIR}/post-install.sh
        @ONLY
    )

    install(PROGRAMS ${CMAKE_BINARY_DIR}/post-install.sh
        DESTINATION ${ALARIS_INSTALL_PATHS_BINDIR}
        COMPONENT PostInstall
    )
endfunction()

# Uninstall target
function(setup_uninstall)
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
endfunction()

# Execute installation steps
install_alaris_targets()
install_alaris_files()
install_system_integration()
setup_post_install()
setup_uninstall()

# Print installation summary
message(STATUS "Installation configuration:")
message(STATUS "  Prefix:        ${CMAKE_INSTALL_PREFIX}")
message(STATUS "  Binaries:      ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_BINDIR}")
message(STATUS "  Libraries:     ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_LIBDIR}")
message(STATUS "  Headers:       ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_INCLUDEDIR}")
message(STATUS "  Config:        ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_CONFIGDIR}")
message(STATUS "  Data:          ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_DATADIR}")
message(STATUS "  Documentation: ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_PATHS_DOCDIR}") 