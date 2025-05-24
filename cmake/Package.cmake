# cmake/Package.cmake
# CPack configuration for Alaris packaging

include(InstallRequiredSystemLibraries)

# Basic package information
set(CPACK_PACKAGE_NAME "Alaris")
set(CPACK_PACKAGE_VERSION_MAJOR ${PROJECT_VERSION_MAJOR})
set(CPACK_PACKAGE_VERSION_MINOR ${PROJECT_VERSION_MINOR})
set(CPACK_PACKAGE_VERSION_PATCH ${PROJECT_VERSION_PATCH})
set(CPACK_PACKAGE_VERSION ${PROJECT_VERSION})

set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "High-Performance Derivatives Trading System")
set(CPACK_PACKAGE_DESCRIPTION "Alaris is a production-grade derivatives trading system designed for volatility arbitrage using American options with process isolation and deterministic execution.")

set(CPACK_PACKAGE_VENDOR "Alaris Trading Systems")
set(CPACK_PACKAGE_CONTACT "admin@alaris-trading.com")
set(CPACK_PACKAGE_HOMEPAGE_URL "https://github.com/alaris-trading/alaris")

# Package file naming
set(CPACK_PACKAGE_FILE_NAME "${CPACK_PACKAGE_NAME}-${CPACK_PACKAGE_VERSION}-${CMAKE_SYSTEM_NAME}-${CMAKE_SYSTEM_PROCESSOR}")

# Resource files
set(CPACK_RESOURCE_FILE_LICENSE "${CMAKE_SOURCE_DIR}/LICENSE")
set(CPACK_RESOURCE_FILE_README "${CMAKE_SOURCE_DIR}/README.md")

# Package metadata
set(CPACK_PACKAGE_CHECKSUM SHA256)
set(CPACK_PACKAGE_RELOCATABLE TRUE)

# Source package configuration
set(CPACK_SOURCE_PACKAGE_FILE_NAME "${CPACK_PACKAGE_NAME}-${CPACK_PACKAGE_VERSION}-source")
set(CPACK_SOURCE_IGNORE_FILES
    "/\\.git/"
    "/build/"
    "/\\.vscode/"
    "/\\.idea/"
    "\\.swp$"
    "\\.orig$"
    "/CMakeLists\\.txt\\.user$"
    "/Makefile$"
    "/CMakeCache\\.txt$"
    "/CMakeFiles/"
    "/cmake_install\\.cmake$"
    "/CTestTestfile\\.cmake$"
    "/Testing/"
    "/_CPack_Packages/"
    "/\\.DS_Store$"
    "/Thumbs\\.db$"
)

# Platform-specific package configuration
if(WIN32)
    configure_windows_package()
elseif(APPLE)
    configure_macos_package()
elseif(UNIX)
    configure_linux_package()
endif()

function(configure_windows_package)
    # Windows-specific packaging with NSIS
    set(CPACK_GENERATOR "NSIS;ZIP" PARENT_SCOPE)
    
    # NSIS configuration
    set(CPACK_NSIS_PACKAGE_NAME "Alaris Trading System" PARENT_SCOPE)
    set(CPACK_NSIS_DISPLAY_NAME "Alaris Trading System ${PROJECT_VERSION}" PARENT_SCOPE)
    set(CPACK_NSIS_HELP_LINK "https://github.com/alaris-trading/alaris" PARENT_SCOPE)
    set(CPACK_NSIS_URL_INFO_ABOUT "https://github.com/alaris-trading/alaris" PARENT_SCOPE)
    set(CPACK_NSIS_CONTACT "admin@alaris-trading.com" PARENT_SCOPE)
    
    # Installation directory
    set(CPACK_NSIS_INSTALL_ROOT "$PROGRAMFILES64" PARENT_SCOPE)
    set(CPACK_NSIS_PACKAGE_NAME "Alaris" PARENT_SCOPE)
    
    # Start menu shortcuts
    set(CPACK_NSIS_MENU_LINKS
        "bin/quantlib_process.exe" "Alaris QuantLib Process"
        "share/doc/alaris/README.md" "Documentation"
        PARENT_SCOPE
    )
    
    # Registry entries
    set(CPACK_NSIS_MODIFY_PATH ON PARENT_SCOPE)
    
    # Custom NSIS commands
    set(CPACK_NSIS_EXTRA_INSTALL_COMMANDS "
        WriteRegStr HKLM 'SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Alaris' 'DisplayName' 'Alaris Trading System'
        WriteRegStr HKLM 'SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Alaris' 'DisplayVersion' '${PROJECT_VERSION}'
        " PARENT_SCOPE)
    
    set(CPACK_NSIS_EXTRA_UNINSTALL_COMMANDS "
        DeleteRegKey HKLM 'SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall\\\\Alaris'
        " PARENT_SCOPE)
endfunction()

function(configure_macos_package)
    # macOS-specific packaging
    set(CPACK_GENERATOR "DragNDrop;TGZ" PARENT_SCOPE)
    
    # Bundle configuration
    set(CPACK_BUNDLE_NAME "Alaris" PARENT_SCOPE)
    set(CPACK_BUNDLE_ICON "${CMAKE_SOURCE_DIR}/resources/icons/alaris.icns" PARENT_SCOPE)
    set(CPACK_BUNDLE_PLIST "${CMAKE_SOURCE_DIR}/deployment/macos/Info.plist" PARENT_SCOPE)
    
    # DMG configuration
    set(CPACK_DMG_VOLUME_NAME "Alaris Trading System" PARENT_SCOPE)
    set(CPACK_DMG_BACKGROUND_IMAGE "${CMAKE_SOURCE_DIR}/resources/dmg/background.png" PARENT_SCOPE)
    set(CPACK_DMG_DS_STORE_SETUP_SCRIPT "${CMAKE_SOURCE_DIR}/deployment/macos/setup_dmg.applescript" PARENT_SCOPE)
    
    # Code signing (if certificates are available)
    if(DEFINED ENV{APPLE_CODESIGN_IDENTITY})
        set(CPACK_BUNDLE_APPLE_CERT_APP "$ENV{APPLE_CODESIGN_IDENTITY}" PARENT_SCOPE)
    endif()
endfunction()

function(configure_linux_package)
    # Linux distribution detection
    if(EXISTS /etc/os-release)
        file(STRINGS /etc/os-release DISTRO_INFO)
        foreach(line ${DISTRO_INFO})
            if(line MATCHES "^ID=(.+)")
                set(LINUX_DISTRO ${CMAKE_MATCH_1})
                string(REPLACE "\"" "" LINUX_DISTRO ${LINUX_DISTRO})
                break()
            endif()
        endforeach()
    endif()
    
    # Set generators based on distribution
    if(LINUX_DISTRO MATCHES "ubuntu|debian")
        set(CPACK_GENERATOR "DEB;TGZ" PARENT_SCOPE)
        configure_deb_package()
    elseif(LINUX_DISTRO MATCHES "centos|rhel|fedora")
        set(CPACK_GENERATOR "RPM;TGZ" PARENT_SCOPE)
        configure_rpm_package()
    else()
        set(CPACK_GENERATOR "TGZ" PARENT_SCOPE)
    endif()
endfunction()

function(configure_deb_package)
    # Debian/Ubuntu package configuration
    set(CPACK_DEBIAN_PACKAGE_MAINTAINER "Alaris Trading Systems <admin@alaris-trading.com>" PARENT_SCOPE)
    set(CPACK_DEBIAN_PACKAGE_SECTION "misc" PARENT_SCOPE)
    set(CPACK_DEBIAN_PACKAGE_PRIORITY "optional" PARENT_SCOPE)
    set(CPACK_DEBIAN_PACKAGE_HOMEPAGE "https://github.com/alaris-trading/alaris" PARENT_SCOPE)
    
    # Dependencies
    set(CPACK_DEBIAN_PACKAGE_DEPENDS 
        "libboost-all-dev (>= 1.75), libc6 (>= 2.28), libstdc++6 (>= 10)"
        PARENT_SCOPE
    )
    
    # Recommends and suggests
    set(CPACK_DEBIAN_PACKAGE_RECOMMENDS "docker.io, docker-compose" PARENT_SCOPE)
    set(CPACK_DEBIAN_PACKAGE_SUGGESTS "prometheus, grafana" PARENT_SCOPE)
    
    # Control scripts
    set(CPACK_DEBIAN_PACKAGE_CONTROL_EXTRA 
        "${CMAKE_SOURCE_DIR}/deployment/debian/postinst;${CMAKE_SOURCE_DIR}/deployment/debian/prerm"
        PARENT_SCOPE
    )
    
    # Generate shlibs
    set(CPACK_DEBIAN_PACKAGE_GENERATE_SHLIBS ON PARENT_SCOPE)
    set(CPACK_DEBIAN_PACKAGE_SHLIBDEPS ON PARENT_SCOPE)
endfunction()

function(configure_rpm_package)
    # RPM package configuration
    set(CPACK_RPM_PACKAGE_SUMMARY "High-Performance Derivatives Trading System" PARENT_SCOPE)
    set(CPACK_RPM_PACKAGE_LICENSE "Proprietary" PARENT_SCOPE)
    set(CPACK_RPM_PACKAGE_GROUP "Applications/Financial" PARENT_SCOPE)
    set(CPACK_RPM_PACKAGE_URL "https://github.com/alaris-trading/alaris" PARENT_SCOPE)
    set(CPACK_RPM_PACKAGE_VENDOR "Alaris Trading Systems" PARENT_SCOPE)
    
    # Dependencies
    set(CPACK_RPM_PACKAGE_REQUIRES "boost-devel >= 1.75, glibc >= 2.28" PARENT_SCOPE)
    
    # Suggests
    set(CPACK_RPM_PACKAGE_SUGGESTS "docker, docker-compose, prometheus, grafana" PARENT_SCOPE)
    
    # Scripts
    set(CPACK_RPM_POST_INSTALL_SCRIPT_FILE "${CMAKE_SOURCE_DIR}/deployment/rpm/postinstall.sh" PARENT_SCOPE)
    set(CPACK_RPM_PRE_UNINSTALL_SCRIPT_FILE "${CMAKE_SOURCE_DIR}/deployment/rpm/preuninstall.sh" PARENT_SCOPE)
    
    # Exclude debug files from automatic processing
    set(CPACK_RPM_SPEC_MORE_DEFINE "%define _unpackaged_files_terminate_build 0" PARENT_SCOPE)
    set(CPACK_RPM_SPEC_MORE_DEFINE "%define _missing_doc_files_terminate_build 0" PARENT_SCOPE)
endfunction()

# Archive generators configuration
set(CPACK_ARCHIVE_COMPONENT_INSTALL ON)

# 7Z configuration
set(CPACK_7Z_COMPONENT_INSTALL ON)

# Component packaging
set(CPACK_COMPONENTS_GROUPING ALL_COMPONENTS_IN_ONE)

# Custom package commands
add_custom_target(package-source
    COMMAND ${CMAKE_CPACK_COMMAND} --config CPackSourceConfig.cmake
    COMMENT "Creating source package"
    WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
)

add_custom_target(package-components
    COMMAND ${CMAKE_CPACK_COMMAND} -G TGZ -D CPACK_COMPONENTS_GROUPING=IGNORE
    COMMENT "Creating component packages"
    WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
)

# Package testing target
add_custom_target(package-test
    COMMAND ${CMAKE_COMMAND} -E echo "Testing package installation..."
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_BINARY_DIR}/package-test
    COMMAND ${CMAKE_COMMAND} -E chdir ${CMAKE_BINARY_DIR}/package-test
        ${CMAKE_COMMAND} -E tar xzf ${CMAKE_BINARY_DIR}/${CPACK_PACKAGE_FILE_NAME}.tar.gz
    COMMENT "Testing package extraction"
    DEPENDS package
)

# Include CPack
include(CPack)

# Print packaging summary
message(STATUS "Packaging configuration:")
message(STATUS "  Generators: ${CPACK_GENERATOR}")
message(STATUS "  Package name: ${CPACK_PACKAGE_FILE_NAME}")
message(STATUS "  Source package: ${CPACK_SOURCE_PACKAGE_FILE_NAME}")

if(WIN32)
    message(STATUS "  Windows NSIS installer will be created")
elseif(APPLE)
    message(STATUS "  macOS DMG will be created")
elseif(UNIX)
    if(LINUX_DISTRO MATCHES "ubuntu|debian")
        message(STATUS "  Debian package (.deb) will be created")
    elseif(LINUX_DISTRO MATCHES "centos|rhel|fedora")
        message(STATUS "  RPM package (.rpm) will be created")
    endif()
endif()