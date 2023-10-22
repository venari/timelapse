# if $STORAGE_CONNECTION_STRING is empty, show an error to user

export CONTAINER_NAME=timelapse
export AWS_REGION=ap-southeast-2
export DESTINATION_BUCKET_NAME=sediment-p133


while true; do

    if [[ "$OSTYPE" == "darwin"* ]]; then
        today=$(gdate -I)     || exit -1
        now=$(gdate -Iminutes)     || exit -1
    else
        today=$(date -I)     || exit -1
        now=$(date -Iminutes)     || exit -1
    fi


    # camera_prefixes=(10 11 13 14 15 19 20 21 22 23 24 25)
    #15 retired
    camera_ids=( 10 11 13 14 19 20 21 22 23 24 25)

    # Overide
    # camera_ids=( 19)

    echo Today: $today
    echo Now: $now
    # echo End Date: $enddate
    d="$now"

    for camera_id in "${camera_ids[@]}"
    do
        echo '\n\n\n********************************************************************************************************************'
        echo Date: $d
        echo Camera: $camera_id
        # echo "$camera_id"/"$d"

        # 012345678901234567890123456789
        # .  4 .2 .2 .2 .2
        # 2023-10-17T13:21:36+13:00

        year=${d:0:4}
        month=${d:5:2}
        day=${d:8:2}
        hour=${d:11:2}
        # minute=${d:14:2}

        azure_blob_filename_prefix="$camera_id"_"$year"-"$month"-"$day"_"$hour"
        s3_target_folder="$camera_id"/"$year"/"$month"/"$day"/

        echo $azure_blob_filename_prefix"*"
        echo $s3_target_folder

        source_files=$(az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output tsv --prefix $azure_blob_filename_prefix --query "[].{Name:name}" --num-results 5000)

        # Put into reverse time order
        source_files=$(echo "${source_files[@]}" | tac)

        # to do optimise to bulk upload after figuring out which files 
        # need to be copied.
        # Maybe use aws s3 sync

        aws_s3_files_already_copied=$(aws s3 ls s3://$DESTINATION_BUCKET_NAME/$s3_target_folder | awk '{print $4}')

        for source_file in $source_files
        do

            if [[ ${aws_s3_files_already_copied[*]} =~ $source_file ]]; then
                printf "."
            else
                echo .
                echo "File $source_file does not exist in S3"

                # Download locally so we can upload to s3

                local_file_path=$s3_target_folder$source_file

                if [ -f "$local_file_path" ]; then
                    echo "$source_file exists locally"
                else 
                    mkdir -p $s3_target_folder
                    # echo "$source_file does not exist."
                    az storage blob download -f $local_file_path --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --name $source_file --overwrite false --output tsv
                fi

                # Upload to s3
                echo Uploading to s3://$DESTINATION_BUCKET_NAME/$s3_target_folder$source_file...
                aws s3 cp $local_file_path s3://$DESTINATION_BUCKET_NAME/$s3_target_folder$source_file --acl private
            fi

        done


    done

done