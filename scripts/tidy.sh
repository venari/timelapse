
# Scan current folder and group files by date
# Create folder if one doesn't already exist for each date named in form 'YYYY-MM-DD'
# Move all files for that date into the newly created folder

# Scan current folder and group files by date
for file in *; do
    # Get the date of the file
    # date=$(stat -f "%Sm" -t "%Y-%m-%d" "$file")

    # Filename is in form "nn_YYYY-MM-DD_..."
    # Get the date from the filename
    # date=$(echo "$file" | cut -d_ -f2)


    date=$(echo "$file" | cut -d_ -f2)
    # echo $file
    # echo $date
    # read -p "Press any key to resume ..."

    # Create a folder for that date if one doesn't already exist
    mkdir -p "$date"
    # Move the file into the folder
    mv "$file" "$date"
done
