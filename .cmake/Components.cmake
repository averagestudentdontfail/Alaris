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
    
    # Automated setup
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND ALARIS_AUTO_SETUP_SCRIPT)
        add_custom_command(TARGET alaris POST_BUILD
            COMMAND bash "${ALARIS_AUTO_SETUP_SCRIPT}"
            VERBATIM
        )
    endif()
endfunction()