Unfortunately, the espressif ESP-IDF extension isn't working well with my VS Code installation, so I'm just using the ESP-IDF cli

To do so, first run the export shell script:
```bash
export ~/.espressif/v6.0.2/esp-idf/export.sh
```

And then the `idf.py` executable will be accessible in the terminal
These commands will be useful:
```bash
idf.py create-project PROJ-NAME # creates a new project in a subdirectory of the cwd
idf.py set-target esp32s3       # sets the target chip
idf.py build                    # builds the project
idf.py flash                    # flashes the project
idf.py -p /dev/ttyACM4 monitor  # monitors the serial port through usb uart
```

# Misc
Was having difficulties building the xtensa library with `idf.py build`, so switched the build system from Ninja to make by
```bash
idf.py fullclean
idf.py -G "Unix Makefiles" build
```