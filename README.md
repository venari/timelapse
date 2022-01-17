# timelapse
A set of tools/scripts to automate the taking and creation of timelapse videos and videos with a Raspberry Pi

# Setup

pip3 install py-cpuinfo


# On board timelapse generation

ffmpeg -r 30 -f image2 -pattern_type glob -i "./<YYYY-MM-DD>*.jpg" -s 1014x760 -vcodec libx264 <YYYY-MM-DD>.mp4


# API Setup
Prerequisites:
- [Dotnet 6](https://dotnet.microsoft.com/en-us/download)
- [EFCore Tooling](https://docs.microsoft.com/en-au/ef/)
```
brew install dotnet-sdk
brew install pgadmin4
dotnet tool install --global dotnet-ef
```

Postgres DB Server:
```

docker pull postgres

mkdir ${HOME}/postgres-data/


docker run -d \
	--name dev-postgres \
	-e POSTGRES_PASSWORD=Pass2020! \
	-v ${HOME}/postgres-data/:/var/lib/postgresql/data \
        -p 5432:5432 \
        postgres


docker exec -it dev-postgres bash        
```

User Secrets:
```
dotnet user-secrets --project timelapse.api init
dotnet user-secrets --project timelapse.api set "ConnectionStrings:DefaultConnection" "Server=tcp:localhost,5432;User ID=postgres;Password=Pass2020!;Database=timelapse"
```


When making changes to the objects in the code, add migrations and update the database script:
```
dotnet ef --project timelapse.api migrations add --context "AppDbContext" <migrationName>
dotnet ef --project timelapse.api migrations script -i --context "AppDbContext" -o timelapse.api/Migrations/scripts.sql
```