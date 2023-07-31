# Scan current folder for files in the format xx_YYYY-MM-DD_hhmmss.jpg
# where xx is a device number, YYYY-MM-DD is the date and hhmmss is the time
# Create a folder for each device that doesn't exist
# For each device, create a sub folder structure in the format YYYY/MM/DD
# Move the files into the sub folder structure
#
# This script is designed to be run in an S3 bucket containing the files

# Set the date format
dateformat="%Y-%m-%d"

# Name the S3 bucket
bucket="s3://sediment-p133"

# Scan the root fo the bucket and group by device number, year, month and day
for file in $(aws s3 ls "$bucket" | awk '{print $4}'); do
    # Get the device number from the filename
    device=$(echo "$file" | cut -d_ -f1)
    # Get the date from the filename
    date=$(echo "$file" | cut -d_ -f2)
    # Get the year from the date
    year=$(echo "$date" | cut -d- -f1)
    # Get the month from the date
    month=$(echo "$date" | cut -d- -f2)
    # Get the day from the datee
    day=$(echo "$date" | cut -d- -f3)

    echo "Would move $file to $bucket/$device/$year/$month/$day"

    # Create a folder for that device if one doesn't already exist
    aws s3 ls "$bucket/$device/" || aws s3 mb "$bucket/$device"
    # Create a folder for that year if one doesn't already exist
    aws s3 ls "$bucket/$device/$year/" || aws s3 mb "$bucket/$device/$year"
    # Create a folder for that month if one doesn't already exist
    aws s3 ls "$bucket/$device/$year/$month/" || aws s3 mb "$bucket/$device/$year/$month"
    # Create a folder for that day if one doesn't already exist
    aws s3 ls "$bucket/$device/$year/$month/$day/" || aws s3 mb "$bucket/$device/$year/$month/$day"
    # Move the file into the folder
    aws s3 mv "$bucket/$file" "$bucket/$device/$year/$month/$day/"

    # Output a message to the console
    echo "Moved $file to $bucket/$device/$year/$month/$day"

done




# s3://sediment-p133/10/2023/06/11/
# s3://sediment-p133/10/2023/06/11/




# for file in *; do
#     # Get the device number from the filename
#     device=$(echo "$file" | cut -d_ -f1)
#     # Get the date from the filename
#     date=$(echo "$file" | cut -d_ -f2)
#     # Get the year from the date
#     year=$(echo "$date" | cut -d- -f1)
#     # Get the month from the date
#     month=$(echo "$date" | cut -d- -f2)
#     # Get the day from the date
#     day=$(echo "$date" | cut -d- -f3)

#     # Create a folder for that device if one doesn't already exist
#     aws s3 ls "$bucket/$device" || aws s3 mb "$bucket/$device"
#     # Create a folder for that year if one doesn't already exist
#     aws s3 ls "$bucket/$device/$year" || aws s3 mb "$bucket/$device/$year"
#     # Create a folder for that month if one doesn't already exist
#     aws s3 ls "$bucket/$device/$year/$month" || aws s3 mb "$bucket/$device/$year/$month"
#     # Create a folder for that day if one doesn't already exist
#     aws s3 ls "$bucket/$device/$year/$month/$day" || aws s3 mb "$bucket/$device/$year/$month/$day"
#     # Move the file into the folder
#     aws s3 mv "$file" "$bucket/$device/$year/$month/$day"

#     break;
