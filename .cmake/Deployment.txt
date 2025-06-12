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

# Install executables with proper permissions
function(install_alaris_executables)
    # Install main executables
    install(TARGETS alaris quantlib-process alaris-config alaris-system
            RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR}
            COMPONENT Runtime
            PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE
                       GROUP_READ GROUP_EXECUTE
                       WORLD_READ WORLD_EXECUTE)
    
    # FIXED: Only install the library, don't export it to avoid dependency issues
    install(TARGETS quantlib
            ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR}
            COMPONENT Development)
            
    message(STATUS "Deployment: Configured executable installation")
endfunction()

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

    # Install generated capability script if available
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND EXISTS "${ALARIS_CAPABILITY_SCRIPT}")
        install(PROGRAMS "${ALARIS_CAPABILITY_SCRIPT}"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT Runtime
                RENAME "alaris-set-capabilities.sh")
    endif()

    # Install generated startup script if available
    if(EXISTS "${CMAKE_BINARY_DIR}/start-alaris.sh")
        install(PROGRAMS "${CMAKE_BINARY_DIR}/start-alaris.sh"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT Runtime
                RENAME "start-alaris.sh")
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

# Create capability setup for installed executables
function(create_production_capability_script)
    if(NOT ALARIS_SET_CAPABILITIES OR NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(PROD_SCRIPT_CONTENT "#!/bin/bash
# Alaris Production Capabilities Setup Script
# For use with installed Alaris trading system

set -e  # Exit on any error

# Installation paths
INSTALL_PREFIX=\"${CMAKE_INSTALL_PREFIX}\"
BIN_DIR=\"\${INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}\"
CONFIG_DIR=\"\${INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}\"

echo \"Setting up Linux capabilities for installed Alaris system...\"
echo \"Installation: \$INSTALL_PREFIX\"

# Check if running as root
if [[ \$EUID -eq 0 ]]; then
    echo \"Error: Do not run this script as root. Use sudo when prompted.\"
    exit 1
fi

# Function to set capabilities for an executable
set_caps() {
    local exe=\"\$1\"
    local caps=\"\$2\"
    local description=\"\$3\"
    
    if [[ ! -f \"\$exe\" ]]; then
        echo \"Warning: \$exe not found, skipping...\"
        return 0
    fi
    
    echo \"Setting capabilities for \$description...\"
    echo \"  Executable: \$exe\"
    echo \"  Capabilities: \$caps\"
    
    # Remove existing capabilities first
    sudo setcap -r \"\$exe\" 2>/dev/null || true
    
    # Set new capabilities
    if sudo setcap \"\$caps\" \"\$exe\"; then
        echo \"  ✓ Success\"
        
        # Verify capabilities were set
        local current_caps=\$(getcap \"\$exe\" 2>/dev/null || echo \"none\")
        echo \"  Verified: \$current_caps\"
    else
        echo \"  ✗ Failed to set capabilities\"
        return 1
    fi
    
    echo
}

# Set capabilities for installed executables
set_caps \"\${BIN_DIR}/quantlib-process\" \"${ALARIS_QUANTLIB_CAPABILITIES}\" \"QuantLib Process\"
set_caps \"\${BIN_DIR}/alaris\" \"${ALARIS_QUANTLIB_CAPABILITIES}\" \"Main Alaris Process\"

echo \"Production capabilities setup completed successfully!\"
echo
echo \"You can now run the system as a regular user:\"
echo \"  \${BIN_DIR}/quantlib-process \${CONFIG_DIR}/quantlib_process.yaml\"
echo \"  dotnet \${BIN_DIR}/Alaris.Lean.dll\"
echo
echo \"To verify capabilities: getcap \${BIN_DIR}/quantlib-process\"
echo \"To remove capabilities: sudo setcap -r \${BIN_DIR}/quantlib-process\"

# Create systemd service if systemd is available
if command -v systemctl >/dev/null 2>&1; then
    echo
    echo \"Creating systemd service file...\"
    
    SERVICE_FILE=\"/tmp/alaris-trading.service\"
    cat > \"\$SERVICE_FILE\" << 'EOFSERVICE'
[Unit]
Description=Alaris Trading System
After=network.target
Requires=network.target

[Service]
Type=forking
User=alaris
Group=alaris
ExecStartPre=/bin/rm -f /dev/shm/alaris_*
ExecStart=${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/start-alaris.sh
ExecStop=/bin/kill -TERM \$MAINPID
Restart=always
RestartSec=10
LimitNOFILE=65536
LimitMEMLOCK=infinity

# Environment
Environment=ALARIS_CONFIG_DIR=${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}
Environment=ALARIS_DATA_DIR=/var/lib/alaris/data

# Security
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/alaris /dev/shm

[Install]
WantedBy=multi-user.target
EOFSERVICE

    echo \"Service file created at: \$SERVICE_FILE\"
    echo \"To install the service:\"
    echo \"  sudo cp \$SERVICE_FILE /etc/systemd/system/\"
    echo \"  sudo systemctl daemon-reload\"
    echo \"  sudo systemctl enable alaris-trading\"
    echo \"  sudo systemctl start alaris-trading\"
fi
")
    
    set(PROD_SCRIPT_PATH "${CMAKE_BINARY_DIR}/alaris-production-setup.sh")
    file(WRITE "${PROD_SCRIPT_PATH}" "${PROD_SCRIPT_CONTENT}")
    
    # Make script executable
    execute_process(
        COMMAND chmod +x "${PROD_SCRIPT_PATH}"
        ERROR_QUIET
    )
    
    # Install the production setup script
    install(PROGRAMS "${PROD_SCRIPT_PATH}"
            DESTINATION "${ALARIS_INSTALL_BINDIR}"
            COMPONENT Runtime
            RENAME "alaris-production-setup.sh")
    
    message(STATUS "Deployment: Created production capability setup script")
endfunction()

# System integration (e.g., systemd services, desktop files)
function(setup_system_integration)
    if(UNIX AND NOT APPLE)
        # Create a comprehensive systemd service template
        set(SERVICE_CONTENT 
"[Unit]
Description=Alaris Trading System - QuantLib Process
After=network.target
Requires=network.target

[Service]
Type=simple
User=alaris
Group=alaris
ExecStartPre=/bin/rm -f /dev/shm/alaris_*
ExecStart=${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/quantlib-process ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}/quantlib_process.yaml
ExecStop=/bin/kill -TERM \$MAINPID
Restart=always
RestartSec=5
TimeoutStartSec=30
TimeoutStopSec=30

# Resource limits for real-time performance
LimitNOFILE=65536
LimitMEMLOCK=infinity

# Security settings
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/alaris /dev/shm

# Environment
Environment=ALARIS_CONFIG_DIR=${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}
Environment=ALARIS_DATA_DIR=/var/lib/alaris/data
Environment=ALARIS_LOG_LEVEL=INFO

[Install]
WantedBy=multi-user.target")

        file(WRITE "${CMAKE_BINARY_DIR}/alaris-quantlib.service" "${SERVICE_CONTENT}")
        
        install(FILES "${CMAKE_BINARY_DIR}/alaris-quantlib.service"
                DESTINATION "lib/systemd/system"
                COMPONENT SystemIntegration
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
                OPTIONAL)
        
        # Create Lean service as well
        set(LEAN_SERVICE_CONTENT
"[Unit]
Description=Alaris Trading System - Lean Process
After=network.target alaris-quantlib.service
Requires=alaris-quantlib.service

[Service]
Type=simple
User=alaris
Group=alaris
ExecStart=/usr/bin/dotnet ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/Alaris.Lean.dll
ExecStop=/bin/kill -TERM \$MAINPID
Restart=always
RestartSec=5
TimeoutStartSec=30
TimeoutStopSec=30

# Resource limits
LimitNOFILE=65536

# Security settings
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/alaris /dev/shm

# Environment
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Environment=ALARIS_CONFIG_DIR=${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}

[Install]
WantedBy=multi-user.target")

        file(WRITE "${CMAKE_BINARY_DIR}/alaris-lean.service" "${LEAN_SERVICE_CONTENT}")
        
        install(FILES "${CMAKE_BINARY_DIR}/alaris-lean.service"
                DESTINATION "lib/systemd/system"
                COMPONENT SystemIntegration
                PERMISSIONS OWNER_READ OWNER_WRITE GROUP_READ WORLD_READ
                OPTIONAL)
                
        message(STATUS "Deployment: Created systemd service files")
    endif()
endfunction()

# Post-install script setup
function(setup_post_install_script)
    set(POST_INSTALL_CONTENT "#!/bin/bash
# Alaris Post-Installation Setup Script

echo \"Alaris Trading System Post-Installation Setup\"
echo \"=============================================\"

# Create alaris user and group if they don't exist
if ! id \"alaris\" &>/dev/null; then
    echo \"Creating alaris user and group...\"
    sudo useradd -r -s /bin/false -d /var/lib/alaris -c \"Alaris Trading System\" alaris
fi

# Create necessary directories
echo \"Creating system directories...\"
sudo mkdir -p /var/lib/alaris/{data,logs,cache}
sudo mkdir -p /var/log/alaris
sudo chown -R alaris:alaris /var/lib/alaris /var/log/alaris

# Set up log rotation
echo \"Setting up log rotation...\"
sudo tee /etc/logrotate.d/alaris > /dev/null << 'EOFLOGROTATE'
/var/log/alaris/*.log {
    daily
    missingok
    rotate 30
    compress
    delaycompress
    notifempty
    create 644 alaris alaris
    postrotate
        systemctl reload alaris-quantlib.service > /dev/null 2>&1 || true
        systemctl reload alaris-lean.service > /dev/null 2>&1 || true
    endscript
}
EOFLOGROTATE

# Set up real-time capabilities if requested
if [[ \"\${1:-}\" == \"--with-capabilities\" ]]; then
    echo \"Setting up real-time capabilities...\"
    \"${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris-production-setup.sh\"
fi

# Set up systemd services if available
if command -v systemctl >/dev/null 2>&1; then
    echo \"Setting up systemd services...\"
    sudo systemctl daemon-reload
    echo \"Services installed. To enable and start:\"
    echo \"  sudo systemctl enable alaris-quantlib.service\"
    echo \"  sudo systemctl enable alaris-lean.service\"
    echo \"  sudo systemctl start alaris-quantlib.service\"
    echo \"  sudo systemctl start alaris-lean.service\"
fi

echo
echo \"Post-installation setup completed!\"
echo
echo \"Next steps:\"
echo \"1. Configure trading parameters in ${CMAKE_INSTALL_PREFIX}/${ALARIS_INSTALL_CONFIGDIR}/\"
echo \"2. Set up real-time capabilities: ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris-production-setup.sh\"
echo \"3. Start services or run manually: ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/start-alaris.sh\"
echo \"4. Monitor logs: journalctl -f -u alaris-quantlib.service\"
")
    
    file(WRITE "${CMAKE_BINARY_DIR}/alaris-post-install.sh" "${POST_INSTALL_CONTENT}")
    
    install(PROGRAMS "${CMAKE_BINARY_DIR}/alaris-post-install.sh"
            DESTINATION "${ALARIS_INSTALL_BINDIR}"
            COMPONENT PostInstall
            RENAME "alaris-post-install.sh")
            
    message(STATUS "Deployment: Created post-install script")
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

# Additional cleanup for Alaris-specific items
message(STATUS \"Removing Alaris-specific system files...\")
exec_program(
    \"sudo\" ARGS \"rm -f /etc/systemd/system/alaris-*.service\"
    OUTPUT_VARIABLE rm_out
    RETURN_VALUE rm_retval
)
exec_program(
    \"sudo\" ARGS \"rm -f /etc/logrotate.d/alaris\"
    OUTPUT_VARIABLE rm_out
    RETURN_VALUE rm_retval
)
exec_program(
    \"sudo\" ARGS \"systemctl daemon-reload\"
    OUTPUT_VARIABLE rm_out
    RETURN_VALUE rm_retval
)

message(STATUS \"Uninstall completed. Manual cleanup may be needed for:\")
message(STATUS \"  - User 'alaris' and group 'alaris'\")
message(STATUS \"  - Data directory /var/lib/alaris\")
message(STATUS \"  - Log directory /var/log/alaris\")
")

    file(WRITE "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake" "${UNINSTALL_SCRIPT_CONTENT}")
    
    add_custom_target(uninstall
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake"
        COMMENT "Uninstalling ${PROJECT_NAME} from ${CMAKE_INSTALL_PREFIX}"
    )
    
    message(STATUS "Uninstall target created")
endfunction()

# FIXED: Install development headers if requested - simplified without CMake export
function(install_development_files)
    if(NOT ALARIS_INSTALL_DEVELOPMENT)
        return()
    endif()
    
    # Install headers
    install(FILES ${QUANTLIB_HEADERS_LIST}
            DESTINATION "${ALARIS_INSTALL_INCLUDEDIR}"
            COMPONENT Development)
    
    # Create a simple find script instead of CMake config
    set(FIND_SCRIPT_CONTENT "#!/bin/bash
# Simple Alaris library finder script
# Use this instead of find_package(Alaris) due to external dependency complexity

ALARIS_INSTALL_PREFIX=\"${CMAKE_INSTALL_PREFIX}\"
ALARIS_INCLUDE_DIR=\"\${ALARIS_INSTALL_PREFIX}/${ALARIS_INSTALL_INCLUDEDIR}\"
ALARIS_LIBRARY_DIR=\"\${ALARIS_INSTALL_PREFIX}/${ALARIS_INSTALL_LIBDIR}\"
ALARIS_LIBRARY=\"\${ALARIS_LIBRARY_DIR}/libquantlib.a\"

echo \"Alaris Installation Found:\"
echo \"  Include: \$ALARIS_INCLUDE_DIR\"
echo \"  Library: \$ALARIS_LIBRARY\"
echo
echo \"To use in your CMake project:\"
echo \"  target_include_directories(your_target PRIVATE \$ALARIS_INCLUDE_DIR)\"
echo \"  target_link_libraries(your_target PRIVATE \$ALARIS_LIBRARY)\"
echo \"  # Plus link QuantLib and yaml-cpp as needed\"
")
    
    file(WRITE "${CMAKE_BINARY_DIR}/find-alaris.sh" "${FIND_SCRIPT_CONTENT}")
    
    install(PROGRAMS "${CMAKE_BINARY_DIR}/find-alaris.sh"
            DESTINATION "${ALARIS_INSTALL_BINDIR}"
            COMPONENT Development
            RENAME "find-alaris.sh")
    
    message(STATUS "Deployment: Development files configured for installation")
endfunction()

# Call the functions to set up installations
install_alaris_executables()
install_common_files()
install_development_files()
create_production_capability_script()
setup_system_integration()
setup_post_install_script()
setup_cmake_uninstall_target()

# Print installation summary
message(STATUS "")
message(STATUS "=== Installation Configuration ===")
message(STATUS "Installation Summary (relative to CMAKE_INSTALL_PREFIX='${CMAKE_INSTALL_PREFIX}'):")
message(STATUS "  Binaries:      ${ALARIS_INSTALL_BINDIR}")
message(STATUS "  Libraries:     ${ALARIS_INSTALL_LIBDIR}")
message(STATUS "  Headers:       ${ALARIS_INSTALL_INCLUDEDIR}")
message(STATUS "  Config:        ${ALARIS_INSTALL_CONFIGDIR}")
message(STATUS "  Data:          ${ALARIS_INSTALL_DATADIR}")
message(STATUS "  Documentation: ${ALARIS_INSTALL_DOCDIR}")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "  Capabilities:  Configured")
endif()
message(STATUS "")
message(STATUS "Installation commands:")
message(STATUS "  cmake --install .                    # Install all components")
message(STATUS "  cmake --install . --component Runtime # Install only runtime")
message(STATUS "  cmake --install . --component Development # Install only dev files")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "  # After install, run for capabilities:")
    message(STATUS "  ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris-production-setup.sh")
endif()
message(STATUS "==================================")