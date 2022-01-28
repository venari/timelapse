#!/bin/bash

## Create Web App service

AZURE_RESOURCE_GROUP=timelapse
AZURE_APP_NAME=timelapse-dev
AZURE_APP_PLAN_NAME=timelapse
AZURE_LOCATION=australiasoutheast
DB_SERVER=timelapse
DB_NAME=timelapse-dev
# VNET_NAME=timelapse
# PRIVATE_DNS_ZONE_PREFIX=timelapse
# SUBNET_NAME=timelapse
# SUBNET_PREFIXES=10.0.0.0/24
STORAGE_ACCOUNT_NAME=venaritimelapse
STORAGE_CONTAINER_NAME=timelapse
# KEYVAULT_NAME=timelapse-dev

echo Determining IP Address...
IP_ADDRESS=$(curl ipecho.net/plain)


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


# echo "Checking if VNET $VNET_NAME already exists...."
# MATCHING_COUNT=$(az network vnet list --query "[?name=='$VNET_NAME'].{name:name}.length(@)")
# if [ $MATCHING_COUNT = 0 ]
# then
#     echo "Creating VNET $VNET_NAME..."
#     az network vnet create --name $VNET_NAME --resource-group $AZURE_RESOURCE_GROUP  --location $AZURE_LOCATION
# else
#     echo "VNET $VNET_NAME already exists."
# fi

# echo "Checking if VNET subnet $SUBNET_NAME already exists...."
# MATCHING_COUNT=$(az network vnet subnet list --vnet-name $VNET_NAME --resource-group $AZURE_RESOURCE_GROUP --query "[?name=='$SUBNET_NAME'].{name:name}.length(@)")
# if [ $MATCHING_COUNT = 0 ]
# then
#     echo "Creating VNET subnet $SUBNET_NAME..."
#     az network vnet subnet create  --resource-group $AZURE_RESOURCE_GROUP --vnet-name $VNET_NAME --name $SUBNET_NAME --address-prefixes $SUBNET_PREFIXES      
# else
#     echo "VNET subnet $SUBNET_NAME already exists."
# fi



echo "Checking if webapp $AZURE_APP_NAME already exists...."
MATCHING_COUNT=$(az webapp list --query "[?name=='$AZURE_APP_NAME'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating webapp $AZURE_APP_NAME..."
    az webapp create --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP  --plan $AZURE_APP_PLAN_NAME --runtime "DOTNET|6.0" 
    # --vnet $VNET_NAME --subnet $SUBNET_NAME


    # az webapp deployment github-actions add --name timelapse-dev --repo venari/timelapse --resource-group timelapse --branch development --token ghp_xxx --runtime "DOTNET|6.0"    
    # Command group 'webapp deployment github-actions' is in preview and under development. Reference and support levels: https://aka.ms/CLI_refstatus
    # Verified GitHub repo and branch
    # Runtime DOTNET|6.0 is not supported for GitHub Actions deployments.
    echo "✅"
else
    echo "✔️"
fi


DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
ConnectionStrings__DefaultConnection=$TimelapseDev_ConnectionStrings__DefaultConnection




# az postgres flexible-server create --admin-user $Timelapse_DBadmin_user --admin-password $Timelapse_DBadmin_password --location $AZURE_LOCATION --name $DB_SERVER --public none --resource-group AZURE_RESOURCE_GROUP --tier Burstable --sku-name Standard_B1ms  --storage-size 32  --version 13
# az postgres flexible-server create --admin-user $Timelapse_DBadmin_user --admin-password $Timelapse_DBadmin_password --location $AZURE_LOCATION --name $DB_SERVER --public none --resource-group AZURE_RESOURCE_GROUP --tier Burstable --sku-name Standard_B1ms  --storage-size 32  --version 13
# az postgres flexible-server create --vnet $VNET_NAME --admin-user Timelapse_DBadmin_user --admin-password $Timelapse_DBadmin_password  --name timelapse2 --location $AZURE_LOCATION --resource-group $AZURE_RESOURCE_GROUP --private-dns-zone timelapse --tier Burstable --sku-name Standard_B1ms  --storage-size 32  --version 13 --database-name $DB_NAME --private-dns-zone $PRIVATE_DNS_ZONE_PREFIX.postgres.database.azure.com --subnet $SUBNET_NAME

# az postgres flexible-server db create --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --database-name $DB_NAME

# az postgres flexible-server db list --server-name $DB_SERVER --resource-group timelapse
# az postgres flexible-server db list --server-name $DB_SERVER --resource-group timelapsey
# az postgres flexible-server db create --resource-group timelapse --server-name timelapse --database-name timelapse
# az postgres flexible-server db create --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --database-name $DB_NAME


echo "Checking if PostgreSQL Server $DB_SERVER already exists...."
MATCHING_COUNT=$(az postgres flexible-server list --resource-group $AZURE_RESOURCE_GROUP --query "[?name=='$DB_SERVER'].{name:name}.length(@)")
if [ $MATCHING_COUNT = 0 ]
then
    echo "Creating DB Server $DB_SERVER..."
    az postgres flexible-server create --admin-user $Timelapse_DBadmin_user --admin-password $Timelapse_DBadmin_password  --name $DB_SERVER --location $AZURE_LOCATION  --resource-group $AZURE_RESOURCE_GROUP \
    --database-name $DB_NAME \
    --tier Burstable --sku-name Standard_B1ms  --storage-size 32  --version 13
    # --public-access $IP_ADDRESS
    # --vnet $VNET_NAME --private-dns-zone $PRIVATE_DNS_ZONE_PREFIX.private.postgres.database.azure.com --subnet $SUBNET_NAME
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
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string --name $STORAGE_ACCOUNT_NAME --key primary --query "connectionString")


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
dotnet user-secrets --project timelapse.api set "STORAGE_CONNECTION_STRING" $STORAGE_CONNECTION_STRING
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_ACCOUNT_NAME=$STORAGE_ACCOUNT_NAME
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_CONTAINER_NAME=$STORAGE_CONTAINER_NAME
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_KEY1=$STORAGE_KEY1
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings STORAGE_KEY2=$STORAGE_KEY2



# echo Configuring database firewall entry.... 
# az postgres flexible-server firewall-rule create --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --start-ip-address $IP_ADDRESS --end-ip-address $IP_ADDRESS --rule-name 'create_script'

# Attempt to allow web app through firewall failed...
# WEBAPP_IPADDRESS=`dig +short $AZURE_APP_NAME.azurewebsites.net | awk '{ print $1 }'`
# WEBAPP_IPADDRESS=`host $AZURE_APP_NAME.azurewebsites.net | tail -n 1 | awk '{print $4}'`
# az postgres flexible-server firewall-rule create --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --start-ip-address $WEBAPP_IPADDRESS --end-ip-address $WEBAPP_IPADDRESS --rule-name 'WEBAPP-ACCESS'


# az webapp config connection-string set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --connection-string-type PostgreSQL --settings DefaultConnection="$ConnectionStrings__DefaultConnection" --output none
az webapp config connection-string set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --connection-string-type SQLAzure --settings DefaultConnection="$ConnectionStrings__DefaultConnection" --output none
# az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings ASPNETCORE_ENVIRONMENT=Development --output none
az webapp config appsettings set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --settings ASPNETCORE_ENVIRONMENT=Production --output none

# az webapp config connection-string set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --connection-string-type PostgreSQL --settings DefaultConnection='Host=timelapse.postgres.database.azure.com;Port=5432;User ID={DBUsername};Password={DBPassword};Database=timelapse'
# az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "DBUsername" --value "api"
# az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "DBPassword" --value "<Super Secret Password>"


# az webapp log config --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --application-logging azureblobstorage --docker-container-logging off --detailed-error-messages true --web-server-logging off  --level information
# az webapp log config --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --application-logging azureblobstorage --docker-container-logging filesystem --web-server-logging off  --level information
az webapp log config --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --application-logging azureblobstorage --docker-container-logging filesystem --web-server-logging filesystem  --level information

# Not using Key vault

    # echo "Checking if keyvault $KEYVAULT_NAME already exists...."
    # MATCHING_KEYVAULT_NAME_COUNT=$(az keyvault list --query "[?name=='$KEYVAULT_NAME'].{name:name}.length(@)")
    # if [ $MATCHING_KEYVAULT_NAME_COUNT = 0 ]
    # then
    #     echo "Creating keyvault $KEYVAULT_NAME..."
    #     az keyvault create --name ${KEYVAULT_NAME} --resource-group ${AZURE_RESOURCE_GROUP} --location "$AZURE_LOCATION"
    # else
    #     echo "Keyvault $KEYVAULT_NAME already exists."
    # fi

    # echo "az webapp identity assign...."
    # PRINCIPAL_ID=$(az webapp identity assign --name ${AZURE_APP_NAME}  --resource-group ${AZURE_RESOURCE_GROUP} --query "[principalId]" -o tsv)
    # echo "Granting access to KeyVault $KEYVAULT_NAME..."
    # az keyvault set-policy --name ${KEYVAULT_NAME} --object-id $PRINCIPAL_ID --secret-permissions get list

    # echo "Setting KeyVaultName application setting to $KEYVAULT_NAME..."
    # az webapp config appsettings set --name ${AZURE_APP_NAME} --resource-group ${AZURE_RESOURCE_GROUP} --settings KeyVaultName=$KEYVAULT_NAME

# Not using deployment slot - for keyvault

    # echo "az webapp identity assign for deploy slot...."
    # PRINCIPAL_ID=$(az webapp identity assign --name ${AZURE_APP_NAME}  --slot deploy --resource-group ${AZURE_RESOURCE_GROUP} --query "[principalId]" -o tsv)

    # echo "Granting access to KeyVault $KEYVAULT_NAME for deploy slot..."
    # az keyvault set-policy --name ${KEYVAULT_NAME} --object-id $PRINCIPAL_ID --secret-permissions get list 

