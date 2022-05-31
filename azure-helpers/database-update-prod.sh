#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
source $DIR/database-connection-strings.secret.sh
export ConnectionStrings__DefaultConnection=$Timelapse_ConnectionStrings__DefaultConnection

dotnet ef --project timelapse.api database update --connection "$ConnectionStrings__DefaultConnection"

unset ConnectionStrings__DefaultConnection