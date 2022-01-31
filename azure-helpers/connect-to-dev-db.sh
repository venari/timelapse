#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
ConnectionStrings__DefaultConnection=$TimelapseDev_ConnectionStrings__DefaultConnection

dotnet user-secrets --project timelapse.api set "STORAGE_CONNECTION_STRING" $STORAGE_CONNECTION_STRING