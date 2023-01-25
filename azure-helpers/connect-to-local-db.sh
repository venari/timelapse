#!/bin/bash

dotnet user-secrets --project timelapse.api set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;User ID=postgres;Password=Pass2020!;Database=timelapse"
dotnet user-secrets --project timelapse.api set "SendgridAPIKey" $SendgridAPIKey