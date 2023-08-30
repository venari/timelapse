# if $STORAGE_CONNECTION_STRING is empty, show an error to user

export CONTAINER_NAME=timelapse
export AWS_REGION=ap-southeast-2
export DESTINATION_BUCKET_NAME=sediment-p133

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
camera_ids=( 19)
startdate='2023-08-28'
enddate='2023-08-30'

camera_ids=( 10)
startdate='2023-06-14'
enddate='2023-06-15'

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

        azure_blob_filename_prefix="$camera_id"_"$year"-"$month"-"$day"_
        s3_target_folder="$camera_id"/"$year"/"$month"/"$day"/

        # echo $azure_blob_filename_prefix"*"
        # echo $s3_target_folder

        # az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table --query "[?contains(name,'.jpg')].{Name:name}" --num-results 1000
        # az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table --query "[?contains(name,'$azure_blob_filename_prefix')].{Name:name}" --num-results 1000
        
        # az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table --prefix $azure_blob_filename_prefix --query "[].{Name:name}" --num-results 1000
        source_files=$(az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output tsv --prefix $azure_blob_filename_prefix --query "[].{Name:name}" --num-results 5000)

        echo Checking if any files are in s3 root folder...

        aws_s3_files_in_root_folder=$(aws s3 ls s3://$DESTINATION_BUCKET_NAME/$azure_blob_filename_prefix | awk '{print $4}')

        for aws_s3_file_in_root_folder in $aws_s3_files_in_root_folder
        do
            # echo $aws_s3_file_in_root_folder
            echo Moving $aws_s3_file_in_root_folder to $s3_target_folder$aws_s3_file_in_root_folder
            aws s3 mv s3://$DESTINATION_BUCKET_NAME/$aws_s3_file_in_root_folder s3://$DESTINATION_BUCKET_NAME/$s3_target_folder$aws_s3_file_in_root_folder
        done



        for source_file in $source_files
        do
            # echo $source_file
            # echo $s3_target_folder$source_file

            # check for existence of file in s3
            # aws s3 ls s3://timelapse-images/$s3_target_folder$source_file
            # aws s3 cp $source_file s3://timelapse-images/$s3_target_folder$source_file --acl public-read

            aws s3api head-object --bucket $DESTINATION_BUCKET_NAME --key $s3_target_folder$source_file 2>&1 >/dev/null || NOT_EXIST=true
            if [ $NOT_EXIST ]; then
                echo "File does not exist in S3"

                # Download locally so we can upload to s3
                if [ -f "$source_file" ]; then
                    echo "$source_file exists locally"
                else 
                    # echo "$source_file does not exist."
                    az storage blob download -f $source_file --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --name $source_file --overwrite false --output tsv
                fi

                # Upload to s3
                echo Uploading to s3://$DESTINATION_BUCKET_NAME/$s3_target_folder$source_file...
                aws s3 cp $source_file s3://$DESTINATION_BUCKET_NAME/$s3_target_folder$source_file --acl private
            else
                echo "File $s3_target_folder$source_file already exists in S3"
            fi

        done


        if [[ "$OSTYPE" == "darwin"* ]]; then
            d=$(gdate -I -d "$d + 1 day")
        else
            d=$(date -I -d "$d + 1 day")
        fi

    done



#   echo "Key for fruits array is: $key"
done