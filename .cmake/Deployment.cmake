# .cmake/Deployment.cmake
# Streamlined deployment configuration

include(GNUInstallDirs)

set(ALARIS_INSTALL_BINDIR ${CMAKE_INSTALL_BINDIR})
set(ALARIS_INSTALL_LIBDIR ${CMAKE_INSTALL_LIBDIR})
set(ALARIS_INSTALL_CONFIGDIR "${CMAKE_INSTALL_SYSCONFDIR}/${PROJECT_NAME}")

# Install executables
function(install_alaris_executables)
    install(TARGETS alaris quantlib-process alaris-config alaris-system
            RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR}
            COMPONENT Runtime)
    
    install(TARGETS quantlib
            ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR}
            COMPONENT Development)
endfunction()

# Install configuration files
function(install_common_files)
    if(EXISTS "${CMAKE_SOURCE_DIR}/config")
        install(DIRECTORY "${CMAKE_SOURCE_DIR}/config/"
                DESTINATION "${ALARIS_INSTALL_CONFIGDIR}"
                COMPONENT Configuration
                FILES_MATCHING PATTERN "*.yaml" PATTERN "*.yml" PATTERN "*.json")
    endif()

    if(EXISTS "${CMAKE_BINARY_DIR}/start-alaris.sh")
        install(PROGRAMS "${CMAKE_BINARY_DIR}/start-alaris.sh"
                DESTINATION "${ALARIS_INSTALL_BINDIR}"
                COMPONENT Runtime)
    endif()
endfunction()

# Create production capability script
function(create_production_setup)
    if(NOT ALARIS_SET_CAPABILITIES OR NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(PROD_SCRIPT_CONTENT "#!/bin/bash
set -e
INSTALL_PREFIX=\"${CMAKE_INSTALL_PREFIX}\"
BIN_DIR=\"\${INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}\"
CAPS=\"${ALARIS_QUANTLIB_CAPABILITIES}\"

set_caps() {
    local exe=\"\$1\"
    if [[ -f \"\$exe\" ]]; then
        sudo setcap -r \"\$exe\" 2>/dev/null || true
        sudo setcap \"\$CAPS\" \"\$exe\"
    fi
}

set_caps \"\${BIN_DIR}/quantlib-process\"
set_caps \"\${BIN_DIR}/alaris\"
")
    
    set(PROD_SCRIPT_PATH "${CMAKE_BINARY_DIR}/alaris-production-setup.sh")
    file(WRITE "${PROD_SCRIPT_PATH}" "${PROD_SCRIPT_CONTENT}")
    execute_process(COMMAND chmod +x "${PROD_SCRIPT_PATH}" ERROR_QUIET)
    
    install(PROGRAMS "${PROD_SCRIPT_PATH}"
            DESTINATION "${ALARIS_INSTALL_BINDIR}"
            COMPONENT Runtime)
endfunction()

# Create uninstall target
function(setup_uninstall_target)
    set(UNINSTALL_SCRIPT_CONTENT 
"if(NOT EXISTS \"install_manifest.txt\")
    message(FATAL_ERROR \"Cannot find install manifest\")
endif()

file(READ \"install_manifest.txt\" files)
string(REGEX REPLACE \"\\n\" \";\" files \"\${files}\")

foreach(file \${files})
    if(EXISTS \"\${file}\")
        exec_program(\"${CMAKE_COMMAND}\" ARGS \"-E remove \\\"\${file}\\\"\")
    endif()
endforeach()
")

    file(WRITE "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake" "${UNINSTALL_SCRIPT_CONTENT}")
    
    add_custom_target(uninstall
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_BINARY_DIR}/cmake_uninstall.cmake")
endfunction()

# Execute installation setup
install_alaris_executables()
install_common_files()
create_production_setup()
setup_uninstall_target()