# if $STORAGE_CONNECTION_STRING is empty, show an error to user

export CONTAINER_NAME=timelapse

# az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table
# az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table --query "[?contains(name,'.jpg')].{Name:name}" --num-results 1000


# camera_prefixes=(10 11 13 14 15 19 20 21 22 23 24 25)
#15 retired
camera_ids=( 10 11 13 14 19 20 21 22 23 24 25)

copy_from=2023-08-01

if [[ "$OSTYPE" == "darwin"* ]]; then
    startdate=$(gdate -I -d "$copy_from") || exit -1
    enddate=$(gdate -I)     || exit -1
    enddate=$(gdate -I -d "$enddate + 1 day")
else
    startdate=$(date -I -d "$copy_from") || exit -1
    enddate=$(date -I)     || exit -1
    enddate=$(date -I -d "$enddate + 1 day")
fi


# Overide
startdate='2023-08-28'
enddate'2023-08-30'

echo $startdate
echo $enddate


for camera_id in "${camera_ids[@]}"
do
    # echo $camera_id


    d="$startdate"
    while [ "$d" != "$enddate" ]; do 

        echo $d
        echo $camera_id
        echo "$camera_id"/"$d"

        # year=$(echo $d | cut -d'-' -f1)
        year=${d:0:4}
        month=${d:5:2}
        day=${d:8:2}

        echo "$camera_id"/"$d" "$year" / "$month" / "$day"


    #   cat database.dump | grep $d > $backupFolder/$d.dump
        if [[ "$OSTYPE" == "darwin"* ]]; then
            d=$(gdate -I -d "$d + 1 day")
        else
            d=$(date -I -d "$d + 1 day")
        fi

    done



#   echo "Key for fruits array is: $key"
done