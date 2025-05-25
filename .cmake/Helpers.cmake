# .cmake/Helpers.cmake
# Helper functions for modern CMake patterns

# Function to create a modern library with proper defaults
function(create_modern_library)
    set(options INTERFACE STATIC SHARED)
    set(oneValueArgs NAME OUTPUT_NAME VERSION)
    set(multiValueArgs SOURCES HEADERS PUBLIC_DEPS PRIVATE_DEPS)
    cmake_parse_arguments(LIB "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})
    
    # Determine library type
    if(LIB_INTERFACE)
        add_library(${LIB_NAME} INTERFACE)
    elseif(LIB_SHARED)
        add_library(${LIB_NAME} SHARED ${LIB_SOURCES})
    else()
        add_library(${LIB_NAME} STATIC ${LIB_SOURCES})
    endif()
    
    # Set properties
    if(NOT LIB_INTERFACE)
        target_sources(${LIB_NAME} PRIVATE ${LIB_SOURCES})
        if(LIB_HEADERS)
            target_sources(${LIB_NAME} PUBLIC 
                FILE_SET HEADERS 
                BASE_DIRS ${CMAKE_CURRENT_SOURCE_DIR}
                FILES ${LIB_HEADERS}
            )
        endif()
        
        set_target_properties(${LIB_NAME} PROPERTIES
            CXX_STANDARD ${CMAKE_CXX_STANDARD}
            CXX_STANDARD_REQUIRED ON
            CXX_EXTENSIONS OFF
            POSITION_INDEPENDENT_CODE ON
        )
        
        if(LIB_OUTPUT_NAME)
            set_target_properties(${LIB_NAME} PROPERTIES OUTPUT_NAME ${LIB_OUTPUT_NAME})
        endif()
        
        if(LIB_VERSION)
            set_target_properties(${LIB_NAME} PROPERTIES VERSION ${LIB_VERSION})
        endif()
    endif()
    
    # Link dependencies
    if(LIB_PUBLIC_DEPS)
        target_link_libraries(${LIB_NAME} PUBLIC ${LIB_PUBLIC_DEPS})
    endif()
    
    if(LIB_PRIVATE_DEPS AND NOT LIB_INTERFACE)
        target_link_libraries(${LIB_NAME} PRIVATE ${LIB_PRIVATE_DEPS})
    endif()
    
    # Include directories
    target_include_directories(${LIB_NAME} PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
        $<INSTALL_INTERFACE:include>
    )
endfunction()

# Function to create a modern executable
function(create_modern_executable)
    set(options)
    set(oneValueArgs NAME OUTPUT_NAME)
    set(multiValueArgs SOURCES DEPS)
    cmake_parse_arguments(EXE "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})
    
    add_executable(${EXE_NAME} ${EXE_SOURCES})
    
    set_target_properties(${EXE_NAME} PROPERTIES
        CXX_STANDARD ${CMAKE_CXX_STANDARD}
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    )
    
    if(EXE_OUTPUT_NAME)
        set_target_properties(${EXE_NAME} PROPERTIES OUTPUT_NAME ${EXE_OUTPUT_NAME})
    endif()
    
    if(EXE_DEPS)
        target_link_libraries(${EXE_NAME} PRIVATE ${EXE_DEPS})
    endif()
endfunction()

# Function to setup common compile options
function(setup_compile_options TARGET)
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(${TARGET} PRIVATE
            -Wall -Wextra -Wpedantic
            -Werror=return-type
            -Werror=non-virtual-dtor
            $<$<CONFIG:Debug>:-g -O0>
            $<$<CONFIG:Release>:-O3>
        )
    endif()
endfunction()