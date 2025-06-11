
# DataSetupTarget.cmake - Executed by setup-data target
message(STATUS "Executing data setup...")

# Re-run the main data setup function
include(${CMAKE_CURRENT_SOURCE_DIR}/.cmake/Data.cmake)
setup_alaris_data()
validate_data_setup()

message(STATUS "Data setup completed successfully!")
