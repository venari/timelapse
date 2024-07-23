#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
ConnectionStrings__DefaultConnection=$TimelapseDev_ConnectionStrings__DefaultConnection

dotnet user-secrets --project timelapse.api set "ConnectionStrings:DefaultConnection" "$ConnectionStrings__DefaultConnection"
dotnet user-secrets --project timelapse.api set "SendgridAPIKey" "$SendgridAPIKey"
dotnet user-secrets --project timelapse.api set "ThirdParty_ApiKey" "$ThirdParty_ApiKey"
dotnet user-secrets --project timelapse.api set "LINZApiKey" "$LINZApiKey"

dotnet user-secrets --project extract.imagery set "ConnectionStrings:DefaultConnection" "$ConnectionStrings__DefaultConnection"
# dotnet user-secrets --project extract.imagery set "STORAGE_CONNECTION_STRING" $STORAGE_CONNECTION_STRING