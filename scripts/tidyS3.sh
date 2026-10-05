# Scan current folder for files in the format xx_YYYY-MM-DD_hhmmss.jpg
# where xx is a device number, YYYY-MM-DD is the date and hhmmss is the time
# Create a folder for each device that doesn't exist
# For each device, create a sub folder structure in the format YYYY/MM/DD
# Move the files into the sub folder structure
#
# This script is designed to be run from a local folder containing the files

# Set the date format
dateformat="%Y-%m-%d"

# Scan the current folder and group by device number, year, month and day
for file in *; do
    # Get the device number from the filename
    device=$(echo "$file" | cut -d_ -f1)
    # Get the date from the filename
    date=$(echo "$file" | cut -d_ -f2)
    # Get the year from the date
    year=$(echo "$date" | cut -d- -f1)
    # Get the month from the date
    month=$(echo "$date" | cut -d- -f2)
    # Get the day from the date
    day=$(echo "$date" | cut -d- -f3)

    # Create a folder for that device if one doesn't already exist
    mkdir -p "$device"
    # Create a folder for that year if one doesn't already exist
    mkdir -p "$device/$year"
    # Create a folder for that month if one doesn't already exist
    mkdir -p "$device/$year/$month"
    # Create a folder for that day if one doesn't already exist
    mkdir -p "$device/$year/$month/$day"
    # Move the file into the folder
    mv "$file" "$device/$year/$month/$day"
done

