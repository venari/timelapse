#!/bin/bash

## Create Web App service

AZURE_RESOURCE_GROUP=enviroeyes-dev
AZURE_APP_NAME=enviroeyes-dev2
AZURE_APP_PLAN_NAME=enviroeyes-dev-plan
AZURE_LOCATION=australiasoutheast
DB_SERVER=enviroeyes-dev-db3
DB_NAME=enviroeyes-dev
STORAGE_ACCOUNT_NAME=enviroeyesdevstorage2
STORAGE_CONTAINER_NAME=enviroeyesdevcontainer2

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
ConnectionStrings__DefaultConnection=$EnviroeyesDev_ConnectionStrings__DefaultConnection

echo Determining IP Address...
IP_ADDRESS=$(curl ipecho.net/plain)

# Check if resource group already exists
MATCHING_COUNT=$(az group list --query "[?name=='$AZURE_RESOURCE_GROUP'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating resource group $AZURE_RESOURCE_GROUP in $AZURE_LOCATION..."
    az group create --name $AZURE_RESOURCE_GROUP --location $AZURE_LOCATION
    echo "✅"
else
    echo "✔️"
fi


echo "Checking if app service plan $AZURE_APP_PLAN_NAME already exists...."
MATCHING_COUNT=$(az appservice plan list --query "[?name=='$AZURE_APP_PLAN_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating app service plan $AZURE_APP_PLAN_NAME..."
    az appservice plan create --name $AZURE_APP_PLAN_NAME --resource-group $AZURE_RESOURCE_GROUP --sku F1 --is-linux --location $AZURE_LOCATION
    echo "✅"
else
    echo "✔️"
fi




echo "Checking if webapp $AZURE_APP_NAME already exists...."
MATCHING_COUNT=$(az webapp list --query "[?name=='$AZURE_APP_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating webapp $AZURE_APP_NAME..."
    az webapp create --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP  --plan $AZURE_APP_PLAN_NAME --runtime "DOTNETCORE:8.0" 
    # --vnet $VNET_NAME --subnet $SUBNET_NAME


    # az webapp deployment github-actions add --name timelapse-dev --repo venari/timelapse --resource-group timelapse --branch development --token ghp_xxx --runtime "DOTNET|6.0"    
    # Command group 'webapp deployment github-actions' is in preview and under development. Reference and support levels: https://aka.ms/CLI_refstatus
    # Verified GitHub repo and branch
    # Runtime DOTNET|6.0 is not supported for GitHub Actions deployments.
    echo "✅"
else
    echo "✔️"
fi






echo "Checking if PostgreSQL Server $DB_SERVER already exists...."
MATCHING_COUNT=$(az postgres flexible-server list --resource-group $AZURE_RESOURCE_GROUP --query "[?name=='$DB_SERVER'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating DB Server $DB_SERVER..."
    az postgres flexible-server create --admin-user $Timelapse_DBadmin_user --admin-password $Timelapse_DBadmin_password  --name $DB_SERVER --location $AZURE_LOCATION  --resource-group $AZURE_RESOURCE_GROUP \
    --database-name $DB_NAME \
    --tier Burstable --sku-name Standard_B1ms  --storage-size 32
    echo "✅"
else
    echo "✔️"
fi

echo "Checking if PostgreSQL Database $DB_NAME already exists...."
MATCHING_COUNT=$(az postgres flexible-server db list --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --query "[?name=='$DB_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating Databaase $DB_NAME..."
    az postgres flexible-server db create --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --database-name $DB_NAME
    echo "✅"
else
    echo "✔️"
fi

echo "Checking PostgreSQL firewall rules..."
MATCHING_COUNT=$(az postgres flexible-server firewall-rule list --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --query "[?startIpAddress=='$IP_ADDRESS'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating Firewall Rule AccessFromHome..."
    az postgres flexible-server firewall-rule create --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --start-ip-address $IP_ADDRESS --rule-name 'AccessFromHome'
    echo "✅"
else
    echo "✔️"
fi


MATCHING_COUNT=$(az postgres flexible-server firewall-rule list --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --query "[?startIpAddress=='0.0.0.0'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating Firewall Rule AccessFromAzure..."
    az postgres flexible-server firewall-rule create --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --start-ip-address 0.0.0.0 --rule-name 'AccessFromAzure'
    echo "✅"
else
    echo "✔️"
fi


echo "Running migrations..."
dotnet ef --project timelapse.api database update --connection "$ConnectionStrings__DefaultConnection"


echo "Checking if Storage Account $STORAGE_ACCOUNT_NAME already exists...."
MATCHING_COUNT=$(az storage account list --query "[?name=='$STORAGE_ACCOUNT_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating Storage Account $STORAGE_ACCOUNT_NAME..."
    az storage account create --name $STORAGE_ACCOUNT_NAME --resource-group $AZURE_RESOURCE_GROUP --location $AZURE_LOCATION
    echo "✅"
else
    echo "✔️"
fi

# STORAGE_KEY1=$(az storage account keys list --account-name $STORAGE_ACCOUNT_NAME --resource-group $AZURE_RESOURCE_GROUP --query "[?keyName=='key1'].{value:value}[0].value")
# STORAGE_KEY2=$(az storage account keys list --account-name $STORAGE_ACCOUNT_NAME --resource-group $AZURE_RESOURCE_GROUP --query "[?keyName=='key2'].{value:value}[0].value")
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string --name $STORAGE_ACCOUNT_NAME --resource-group $AZURE_RESOURCE_GROUP --key key1 --query "connectionString" -o tsv)


echo "Checking if Storage Container $STORAGE_CONTAINER_NAME already exists...."
MATCHING_COUNT=$(az storage container list --account-name $STORAGE_ACCOUNT_NAME --connection-string $STORAGE_CONNECTION_STRING --query "[?name=='$STORAGE_CONTAINER_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating Storage Container $STORAGE_CONTAINER_NAME..."
    az storage container create --account-name $STORAGE_ACCOUNT_NAME --name $STORAGE_CONTAINER_NAME --connection-string $STORAGE_CONNECTION_STRING # --public-access blob
    echo "✅"
else
    echo "✔️"
fi

az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_CONNECTION_STRING=$STORAGE_CONNECTION_STRING --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_CONTAINER_NAME=$STORAGE_CONTAINER_NAME --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings SendgridAPIKey=$SendgridAPIKey --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings LINZApiKey=$LINZApiKey --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings ThirdParty_ApiKey=$ThirdParty_ApiKey --output none
dotnet user-secrets --project timelapse.api set "STORAGE_CONNECTION_STRING" $STORAGE_CONNECTION_STRING
dotnet user-secrets --project timelapse.api set "SendgridAPIKey" "$SendgridAPIKey"
dotnet user-secrets --project timelapse.api set "ThirdParty_ApiKey" "$ThirdParty_ApiKey"
dotnet user-secrets --project timelapse.api set "LINZApiKey" "$LINZApiKey"
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_ACCOUNT_NAME=$STORAGE_ACCOUNT_NAME
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_CONTAINER_NAME=$STORAGE_CONTAINER_NAME
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_KEY1=$STORAGE_KEY1
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_KEY2=$STORAGE_KEY2



az webapp config connection-string set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --connection-string-type SQLAzure --settings DefaultConnection="$ConnectionStrings__DefaultConnection" --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings ASPNETCORE_ENVIRONMENT=Production --output none

az webapp log config --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --application-logging azureblobstorage --docker-container-logging filesystem --web-server-logging filesystem  --level information
