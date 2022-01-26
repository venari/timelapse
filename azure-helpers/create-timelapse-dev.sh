#!/bin/bash

## Create Web App service

AZURE_RESOURCE_GROUP=timelapse
AZURE_APP_NAME=timelapse-dev
AZURE_APP_PLAN_NAME=timelapse
AZURE_LOCATION=australiasoutheast
DB_SERVER=timelapse-dev
DB_NAME=timelapse
# KEYVAULT_NAME=timelapse-dev

echo "Checking if app service plan $AZURE_APP_PLAN_NAME already exists...."
MATCHING_APP_PLAN_NAME_COUNT=$(az appservice plan list --query "[?name=='$AZURE_APP_PLAN_NAME'].{name:name}.length(@)")
if [ $MATCHING_APP_PLAN_NAME_COUNT = 0 ]
then
    echo "Creating app service plan $AZURE_APP_PLAN_NAME..."
    az appservice plan create --name $AZURE_APP_PLAN_NAME --resource-group $AZURE_RESOURCE_GROUP --sku F1 --is-linux --location $AZURE_LOCATION
else
    echo "App Service Plan $AZURE_APP_PLAN_NAME already exists."
fi


echo "Checking if webapp $AZURE_APP_NAME already exists...."
MATCHING_APP_NAME_COUNT=$(az webapp list --query "[?name=='$AZURE_APP_NAME'].{name:name}.length(@)")
if [ $MATCHING_APP_NAME_COUNT = 0 ]
then
    echo "Creating webapp $AZURE_APP_NAME..."
    az webapp create --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP  --plan $AZURE_APP_PLAN_NAME --runtime "DOTNET|6.0"


    # az webapp deployment github-actions add --name timelapse-dev --repo venari/timelapse --resource-group timelapse --branch development --token ghp_xxx --runtime "DOTNET|6.0"    
    # Command group 'webapp deployment github-actions' is in preview and under development. Reference and support levels: https://aka.ms/CLI_refstatus
    # Verified GitHub repo and branch
    # Runtime DOTNET|6.0 is not supported for GitHub Actions deployments.
else
    echo "Webapp $AZURE_APP_NAME already exists."
fi


DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
ConnectionStrings__DefaultConnection=$Timelapse_ConnectionStrings__DefaultConnection



# az postgres flexible-server create --admin-user $TimelapseDev_DBadmin_user --admin-password $TimelapseDev_DBadmin_password --location $AZURE_LOCATION --name $DB_SERVER --public none --resource-group timelapse --tier Burstable --sku-name Standard_B1ms  --storage-size 32  --version 13
# az postgres flexible-server db create --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --database-name $DB_NAME

# az postgres flexible-server db list --server-name $DB_SERVER --resource-group timelapse
# az postgres flexible-server db list --server-name $DB_SERVER --resource-group timelapse
# az postgres flexible-server db create --resource-group timelapse --server-name timelapse --database-name timelapse
# az postgres flexible-server db create --resource-group $AZURE_RESOURCE_GROUP --server-name $DB_SERVER --database-name $DB_NAME


# echo Determining IP Address...
# export IP_ADDRESS=$(curl ipecho.net/plain)
# echo Configuring database firewall entry.... 
# az sql server firewall-rule create --name 'database-scaffold-script'  --server $DB_SERVER_NAME --resource-group $AZURE_RESOURCE_GROUP --start-ip-address $IP_ADDRESS --end-ip-address $IP_ADDRESS
# az postgres flexible-server firewall-rule create --name $DB_SERVER --resource-group $AZURE_RESOURCE_GROUP --start-ip-address $IP_ADDRESS --end-ip-address $IP_ADDRESS --rule-name 'create_script'


az webapp config connection-string set --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP --connection-string-type PostgreSQL --settings DefaultConnection="$ConnectionStrings__DefaultConnection"
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

