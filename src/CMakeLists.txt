# src/CMakeLists.txt
message(STATUS "Configuring Alaris C++ components from src/CMakeLists.txt...")
include(GNUInstallDirs)

# Common variables for target properties (can be set in .cmake/BuildSystem.cmake too)
set(ALARIS_CXX_STANDARD ${CMAKE_CXX_STANDARD})
set(ALARIS_CXX_STANDARD_REQUIRED ON)
set(ALARIS_CXX_EXTENSIONS OFF)
set(ALARIS_POSITION_INDEPENDENT_CODE ON)

# --- Core utilities library ---
add_library(alaris_core
    quantlib/core/memory_pool.cpp
    quantlib/core/time_trigger.cpp
    quantlib/core/event_log.cpp
    quantlib/ipc/shared_memory.cpp
    quantlib/ipc/shared_memory_manager.cpp
    # Headers (CMake includes these in source tracking; specify for clarity or if they are not in include path)
    quantlib/core/memory_pool.h
    quantlib/core/time_trigger.h
    quantlib/core/event_log.h
    quantlib/ipc/shared_ring_buffer.h
    quantlib/ipc/shared_memory.h
    quantlib/ipc/message_types.h
)
set_target_properties(alaris_core PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/Libraries" # Simplified folder
)
target_include_directories(alaris_core PUBLIC
    # Consumers will need to include "quantlib/core/header.h" or "core/header.h"
    # This makes "src/quantlib" an include path for alaris_core and its users.
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/quantlib>
    $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}> # Headers installed to <prefix>/include/Alaris/alaris_core etc.
)
target_link_libraries(alaris_core PUBLIC ${YAML_CPP_TARGET} PRIVATE ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_core EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES # Specify public headers for installation, relative to CMAKE_CURRENT_SOURCE_DIR (src/)
    quantlib/core/memory_pool.h quantlib/core/time_trigger.h quantlib/core/event_log.h
    quantlib/ipc/shared_ring_buffer.h quantlib/ipc/shared_memory.h quantlib/ipc/message_types.h
    DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_core # Installs to include/Alaris/alaris_core
    COMPONENT Development)

# --- Pricing engine library ---
add_library(alaris_pricing quantlib/pricing/alo_engine.cpp quantlib/pricing/alo_engine.h)
set_target_properties(alaris_pricing PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/Libraries")
target_include_directories(alaris_pricing PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/quantlib> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}>)
target_link_libraries(alaris_pricing PUBLIC alaris_core PRIVATE ${QUANTLIB_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_pricing EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES quantlib/pricing/alo_engine.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_pricing COMPONENT Development)

# --- Volatility models library ---
add_library(alaris_volatility quantlib/volatility/gjrgarch_wrapper.cpp quantlib/volatility/vol_forecast.cpp quantlib/volatility/gjrgarch_wrapper.h quantlib/volatility/vol_forecast.h)
set_target_properties(alaris_volatility PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/Libraries")
target_include_directories(alaris_volatility PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/quantlib> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}>)
target_link_libraries(alaris_volatility PUBLIC alaris_core PRIVATE ${QUANTLIB_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_volatility EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES quantlib/volatility/gjrgarch_wrapper.h quantlib/volatility/vol_forecast.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_volatility COMPONENT Development)

# --- Trading strategy library ---
add_library(alaris_strategy quantlib/strategy/vol_arb.cpp quantlib/strategy/vol_arb.h)
set_target_properties(alaris_strategy PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/Libraries")
target_include_directories(alaris_strategy PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/quantlib> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}>)
target_link_libraries(alaris_strategy PUBLIC alaris_core alaris_pricing alaris_volatility PRIVATE ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_strategy EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES quantlib/strategy/vol_arb.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_strategy COMPONENT Development)

# --- Main QuantLib process executable ---
add_executable(quantlib_process quantlib/main.cpp)
set_target_properties(quantlib_process PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/Executables"
)
target_link_libraries(quantlib_process PRIVATE
    alaris_core alaris_pricing alaris_volatility alaris_strategy
    ${YAML_CPP_TARGET} ${QUANTLIB_TARGET}
    ${ALARIS_COMMON_LINK_LIBRARIES}
)
# Include quantlib directory for main.cpp if it includes headers like "core/event_log.h"
target_include_directories(quantlib_process PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/quantlib)
install(TARGETS quantlib_process RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE $<$<CONFIG:Release>:-msse4.2;-mavx;-mavx2>)
endif()
if(CMAKE_BUILD_TYPE STREQUAL "Release" AND NOT WIN32)
    target_link_options(quantlib_process PRIVATE -Wl,--no-as-needed)
endif()
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(quantlib_process PRIVATE ALARIS_DEBUG_BUILD ALARIS_ENABLE_PROFILING)
    if(ENABLE_SANITIZERS)
        target_compile_options(quantlib_process PRIVATE -fsanitize=address -fsanitize=undefined -fno-sanitize-recover=all)
        target_link_options(quantlib_process PRIVATE -fsanitize=address -fsanitize=undefined)
    endif()
endif()

# --- Tools ---
add_executable(alaris_config_validator quantlib/tools/config_validator.cpp)
set_target_properties(alaris_config_validator PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/Tools")
target_link_libraries(alaris_config_validator PRIVATE alaris_core ${YAML_CPP_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
target_include_directories(alaris_config_validator PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/quantlib) # If tool sources include Alaris headers
install(TARGETS alaris_config_validator RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

add_executable(alaris_sysinfo quantlib/tools/system_info.cpp)
set_target_properties(alaris_sysinfo PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/Tools")
target_link_libraries(alaris_sysinfo PRIVATE alaris_core ${ALARIS_COMMON_LINK_LIBRARIES})
target_include_directories(alaris_sysinfo PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/quantlib) # If tool sources include Alaris headers
install(TARGETS alaris_sysinfo RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

# Export targets for installation (makes them importable by other CMake projects)
install(EXPORT AlarisTargets
    FILE AlarisTargets.cmake
    NAMESPACE Alaris::
    DESTINATION ${ALARIS_INSTALL_LIBDIR}/cmake/Alaris
    COMPONENT Development
)

# Variables for parent scope (e.g. for root CMakeLists to know about these if src/CMakeLists is included via add_subdirectory)
set(ALARIS_LIBRARIES alaris_core alaris_pricing alaris_volatility alaris_strategy PARENT_SCOPE)
set(ALARIS_EXECUTABLES quantlib_process PARENT_SCOPE)
set(ALARIS_TOOLS alaris_config_validator alaris_sysinfo PARENT_SCOPE)

message(STATUS "Alaris C++ components in src/CMakeLists.txt configuration finished.")