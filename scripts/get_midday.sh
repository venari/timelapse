
# Create a folder called 'midday' if one doesn't already exist
mkdir -p 'midday'

# Iterate all subfolders
for folder in */; do

    echo $folder

    #Scan all files in the subfolder
    for file in "$folder"*.jpg; do

        # echo $file

        # Get the time from the filename
        time=$(echo "$file" | cut -d_ -f3 | cut -d. -f1)
        # echo $time
        # read -p "Press any key to resume ..."

        # if left 3 chars of $time are 120
        if [ "${time:0:3}" = "120" ]; then
            # echo $time
            # echo $folder
            # echo $file

        # If the time is 12:00:00
        # if [ "$time" = "12:00:00" ]; then
            # Copy the file to the 'midday' folder
            cp -n "$file" "midday/"
            # read -p "Press any key to resume ..."
        fi
    done
done