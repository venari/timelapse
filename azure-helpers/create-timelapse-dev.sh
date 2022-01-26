#!/bin/bash

## Create Web App service

AZURE_RESOURCE_GROUP=timelapse
AZURE_APP_NAME=timelapse-dev
AZURE_APP_PLAN_NAME=timelapse
KEYVAULT_NAME=timelapse-dev


echo "Checking if app service plan $AZURE_APP_PLAN_NAME already exists...."
MATCHING_APP_PLAN_NAME_COUNT=$(az appservice plan list --query "[?name=='$AZURE_APP_PLAN_NAME'].{name:name}.length(@)")
if [ $MATCHING_APP_PLAN_NAME_COUNT = 0 ]
then
    echo "Creating app service plan $AZURE_APP_PLAN_NAME..."
    az appservice plan create --name $AZURE_APP_PLAN_NAME --resource-group $AZURE_RESOURCE_GROUP --sku F1 --is-linux --location "australiasoutheast"
else
    echo "App Service Plan $AZURE_APP_PLAN_NAME already exists."
fi


echo "Checking if webapp $AZURE_APP_NAME already exists...."
MATCHING_APP_NAME_COUNT=$(az webapp list --query "[?name=='$AZURE_APP_NAME'].{name:name}.length(@)")
if [ $MATCHING_APP_NAME_COUNT = 0 ]
then
    echo "Creating webapp $AZURE_APP_NAME..."
    az webapp create --name $AZURE_APP_NAME --resource-group $AZURE_RESOURCE_GROUP  --plan $AZURE_APP_PLAN_NAME --runtime "DOTNET|6.0"
else
    echo "Webapp $AZURE_APP_NAME already exists."
fi

echo "Checking if keyvault $KEYVAULT_NAME already exists...."
MATCHING_KEYVAULT_NAME_COUNT=$(az keyvault list --query "[?name=='$KEYVAULT_NAME'].{name:name}.length(@)")
if [ $MATCHING_KEYVAULT_NAME_COUNT = 0 ]
then
    echo "Creating keyvault $KEYVAULT_NAME..."
    az keyvault create --name ${KEYVAULT_NAME} --resource-group ${AZURE_RESOURCE_GROUP} --location "westus"
else
    echo "Keyvault $KEYVAULT_NAME already exists."
fi

echo "az webapp identity assign...."
PRINCIPAL_ID=$(az webapp identity assign --name ${AZURE_APP_NAME}  --resource-group ${AZURE_RESOURCE_GROUP} --query "[principalId]" -o tsv)
echo "Granting access to KeyVault $KEYVAULT_NAME..."
az keyvault set-policy --name ${KEYVAULT_NAME} --object-id $PRINCIPAL_ID --secret-permissions get list

echo "Setting KeyVaultName application setting to $KEYVAULT_NAME..."
az webapp config appsettings set --name ${AZURE_APP_NAME} --resource-group ${AZURE_RESOURCE_GROUP} --settings KeyVaultName=$KEYVAULT_NAME

# echo "az webapp identity assign for deploy slot...."
# PRINCIPAL_ID=$(az webapp identity assign --name ${AZURE_APP_NAME}  --slot deploy --resource-group ${AZURE_RESOURCE_GROUP} --query "[principalId]" -o tsv)

# echo "Granting access to KeyVault $KEYVAULT_NAME for deploy slot..."
# az keyvault set-policy --name ${KEYVAULT_NAME} --object-id $PRINCIPAL_ID --secret-permissions get list 
