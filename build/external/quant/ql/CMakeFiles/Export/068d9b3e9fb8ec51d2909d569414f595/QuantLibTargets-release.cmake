#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "QuantLib::QuantLib" for configuration "Release"
set_property(TARGET QuantLib::QuantLib APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(QuantLib::QuantLib PROPERTIES
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/lib/libQuantLib.so.1.39.0"
  IMPORTED_SONAME_RELEASE "libQuantLib.so.1"
  )

list(APPEND _cmake_import_check_targets QuantLib::QuantLib )
list(APPEND _cmake_import_check_files_for_QuantLib::QuantLib "${_IMPORT_PREFIX}/lib/libQuantLib.so.1.39.0" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
