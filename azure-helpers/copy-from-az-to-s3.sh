# if $STORAGE_CONNECTION_STRING is empty, show an error to user

export CONTAINER_NAME=timelapse
export AWS_REGION=ap-southeast-2
export DESTINATION_BUCKET_NAME=sediment-p133


# az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table
# az storage blob list --container-name=$CONTAINER_NAME --connection-string=$STORAGE_CONNECTION_STRING --output table --query "[?contains(name,'.jpg')].{Name:name}" --num-results 1000


# camera_prefixes=(10 11 13 14 15 19 20 21 22 23 24 25)
#15 retired
camera_ids=( 10 11 13 14 19 20 21 22 23 24 25)

copy_from=2023-07-01

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
# camera_ids=( 19)
# startdate='2023-08-28'
# enddate='2023-08-30'

# camera_ids=( 14)
# startdate='2023-06-11'
# enddate='2023-06-12'

startdate='2023-09-01'
# enddate='2023-06-16'

echo Start Date: $startdate
echo End Date: $enddate


for camera_id in "${camera_ids[@]}"
do
    # echo $camera_id


    d="$startdate"
    while [ "$d" != "$enddate" ]; do 

        echo '\n\n\n********************************************************************************************************************'
        echo Date: $d
        echo Camera: $camera_id
        # echo "$camera_id"/"$d"

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

        # echo Checking if any files are in s3 root folder...
        # aws_s3_files_in_root_folder=$(aws s3 ls s3://$DESTINATION_BUCKET_NAME/$azure_blob_filename_prefix | awk '{print $4}')

        # if aws_s3_files_in_root_folder is not empty, move files
        # to s3_target_folder

        # read -p "Press any key to resume ..."

        # # if [ ${#aws_s3_files_in_root_folder[@]} -eq 0 ]; then
        # # if (( ${#aws_s3_files_in_root_folder[@]} )) ; then
        # # if [[ ${#aws_s3_files_in_root_folder[@]} ]] ; then
        # if [[ ${#aws_s3_files_in_root_folder} -ne 0 ]] ; then
        #     echo Moving files to $s3_target_folder$aws_s3_file_in_root_folder
        #     # This needs a little explaining....
        #     #         Source bucket                         destination buckey and folder               recursive  exclude all, then include matching prefix,       ....but exclude all that are already in a folder
        #     aws s3 mv s3://$DESTINATION_BUCKET_NAME/ s3://$DESTINATION_BUCKET_NAME/$s3_target_folder --recursive --exclude "*" --include "$azure_blob_filename_prefix*" --exclude "*/*"
        # fi


# to do optimise to bulk upload after figuring out which files 
# need to be copied.
# Maybe use aws s3 sync

        aws_s3_files_already_copied=$(aws s3 ls s3://$DESTINATION_BUCKET_NAME/$s3_target_folder | awk '{print $4}')

        for source_file in $source_files
        do
            # echo $source_file
            # echo $s3_target_folder$source_file

            # check for existence of file in s3
            # aws s3 ls s3://timelapse-images/$s3_target_folder$source_file
            # aws s3 cp $source_file s3://timelapse-images/$s3_target_folder$source_file --acl public-read


            # check if aws_s3_files_already_copied contains $source_file

            # aws s3api head-object --bucket $DESTINATION_BUCKET_NAME --key $s3_target_folder$source_file 2>&1 >/dev/null || NOT_EXIST=true

            # echo $aws_s3_files_already_copied
            # echo $source_file
            # echo ${aws_s3_files_already_copied[@]/$source_file

            # [[ ${aws_s3_files_already_copied[*]} =~ $source_file ]] 
            if [[ ${aws_s3_files_already_copied[*]} =~ $source_file ]]; then
            # if [[ "${aws_s3_files_already_copied[@]/$source_file/}" != "${aws_s3_files_already_copied[@]}" ]]; then
                # echo "File $source_file already exists in S3"
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


        if [[ "$OSTYPE" == "darwin"* ]]; then
            d=$(gdate -I -d "$d + 1 day")
        else
            d=$(date -I -d "$d + 1 day")
        fi

    done



#   echo "Key for fruits array is: $key"
done