idf_component_register(SRCS "take_picture.c"
                    INCLUDE_DIRS "."
                    REQUIRES fatfs sdmmc esp_timer nvs_flash)