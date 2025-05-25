# src/quantlib/CMakeLists.txt
message(STATUS "Configuring Alaris QuantLib C++ components...")
include(GNUInstallDirs)

# Common variables for target properties (can be set in BuildSystem.cmake too)
set(ALARIS_CXX_STANDARD ${CMAKE_CXX_STANDARD})
set(ALARIS_CXX_STANDARD_REQUIRED ON)
set(ALARIS_CXX_EXTENSIONS OFF)
set(ALARIS_POSITION_INDEPENDENT_CODE ON)

# --- Core utilities library ---
add_library(alaris_core
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
    ipc/shared_memory.cpp
    ipc/shared_memory_manager.cpp
    # Headers:
    core/memory_pool.h
    core/time_trigger.h
    core/event_log.h
    ipc/shared_ring_buffer.h
    ipc/shared_memory.h
    ipc/message_types.h
)
set_target_properties(alaris_core PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/QuantLib/Libraries"
)
target_include_directories(alaris_core PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}> # For using "core/header.h"
    $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}/alaris_core> # When installed include/Alaris/alaris_core
)
target_link_libraries(alaris_core PUBLIC ${YAML_CPP_TARGET} PRIVATE ${ALARIS_COMMON_LINK_LIBRARIES}) # ALARIS_COMMON_LINK_LIBRARIES from Config.cmake
install(TARGETS alaris_core EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES core/memory_pool.h core/time_trigger.h core/event_log.h ipc/shared_ring_buffer.h ipc/shared_memory.h ipc/message_types.h
        DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_core COMPONENT Development)

# --- Pricing engine library ---
add_library(alaris_pricing pricing/alo_engine.cpp pricing/alo_engine.h)
set_target_properties(alaris_pricing PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/QuantLib/Libraries"
)
target_include_directories(alaris_pricing PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}/alaris_pricing>)
target_link_libraries(alaris_pricing PUBLIC alaris_core PRIVATE ${QUANTLIB_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_pricing EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES pricing/alo_engine.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_pricing COMPONENT Development)

# --- Volatility models library ---
add_library(alaris_volatility volatility/gjrgarch_wrapper.cpp volatility/vol_forecast.cpp volatility/gjrgarch_wrapper.h volatility/vol_forecast.h)
set_target_properties(alaris_volatility PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/QuantLib/Libraries"
)
target_include_directories(alaris_volatility PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}/alaris_volatility>)
target_link_libraries(alaris_volatility PUBLIC alaris_core PRIVATE ${QUANTLIB_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_volatility EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES volatility/gjrgarch_wrapper.h volatility/vol_forecast.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_volatility COMPONENT Development)

# --- Trading strategy library ---
add_library(alaris_strategy strategy/vol_arb.cpp strategy/vol_arb.h)
set_target_properties(alaris_strategy PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS} POSITION_INDEPENDENT_CODE ${ALARIS_POSITION_INDEPENDENT_CODE}
    FOLDER "Alaris/QuantLib/Libraries"
)
target_include_directories(alaris_strategy PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}> $<INSTALL_INTERFACE:${ALARIS_INSTALL_INCLUDEDIR}/alaris_strategy>)
target_link_libraries(alaris_strategy PUBLIC alaris_core alaris_pricing alaris_volatility PRIVATE ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_strategy EXPORT AlarisTargets LIBRARY DESTINATION ${ALARIS_INSTALL_LIBDIR} ARCHIVE DESTINATION ${ALARIS_INSTALL_LIBDIR} RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR})
install(FILES strategy/vol_arb.h DESTINATION ${ALARIS_INSTALL_INCLUDEDIR}/alaris_strategy COMPONENT Development)

# --- Main QuantLib process executable ---
add_executable(quantlib_process main.cpp)
set_target_properties(quantlib_process PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/QuantLib/Executables"
)
target_link_libraries(quantlib_process PRIVATE
    alaris_core alaris_pricing alaris_volatility alaris_strategy
    ${YAML_CPP_TARGET} ${QUANTLIB_TARGET}
    ${ALARIS_COMMON_LINK_LIBRARIES}
)
install(TARGETS quantlib_process RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE $<$<CONFIG:Release>:-msse4.2;-mavx;-mavx2>)
endif()
if(CMAKE_BUILD_TYPE STREQUAL "Release" AND NOT WIN32)
    target_link_options(quantlib_process PRIVATE -Wl,--no-as-needed)
endif()
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(quantlib_process PRIVATE ALARIS_DEBUG_BUILD ALARIS_ENABLE_PROFILING)
    if(ENABLE_SANITIZERS) # From BuildSystem.cmake
        target_compile_options(quantlib_process PRIVATE -fsanitize=address -fsanitize=undefined -fno-sanitize-recover=all)
        target_link_options(quantlib_process PRIVATE -fsanitize=address -fsanitize=undefined)
    endif()
endif()

# --- Tools ---
add_executable(alaris_config_validator tools/config_validator.cpp)
set_target_properties(alaris_config_validator PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/QuantLib/Tools"
)
target_link_libraries(alaris_config_validator PRIVATE alaris_core ${YAML_CPP_TARGET} ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_config_validator RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

add_executable(alaris_sysinfo tools/system_info.cpp)
set_target_properties(alaris_sysinfo PROPERTIES
    CXX_STANDARD ${ALARIS_CXX_STANDARD} CXX_STANDARD_REQUIRED ${ALARIS_CXX_STANDARD_REQUIRED} CXX_EXTENSIONS ${ALARIS_CXX_EXTENSIONS}
    FOLDER "Alaris/QuantLib/Tools"
)
target_link_libraries(alaris_sysinfo PRIVATE alaris_core ${ALARIS_COMMON_LINK_LIBRARIES})
install(TARGETS alaris_sysinfo RUNTIME DESTINATION ${ALARIS_INSTALL_BINDIR} COMPONENT Runtime)

# Create an export set for find_package downstream
install(EXPORT AlarisTargets
    FILE AlarisTargets.cmake
    NAMESPACE Alaris::
    DESTINATION ${ALARIS_INSTALL_LIBDIR}/cmake/Alaris # e.g. lib/cmake/Alaris
    COMPONENT Development
)

# Variables for parent scope (e.g. for root CMakeLists to know about these)
set(ALARIS_QUANTLIB_LIBRARIES alaris_core alaris_pricing alaris_volatility alaris_strategy PARENT_SCOPE)
set(ALARIS_QUANTLIB_EXECUTABLES quantlib_process PARENT_SCOPE)
set(ALARIS_QUANTLIB_TOOLS alaris_config_validator alaris_sysinfo PARENT_SCOPE)

message(STATUS "Alaris QuantLib C++ components configuration finished.")