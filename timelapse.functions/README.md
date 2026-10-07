```
# Install Azure Functions Core Tools v4
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
curl https://packages.microsoft.com/keys/microsoft-2025.asc | gpg --dearmor > microsoft-2025.gpg
sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg
sudo mv microsoft-2025.gpg /etc/apt/trusted.gpg.d/microsoft-2025.gpg
sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs)-prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt-get update
sudo apt-get install azure-functions-core-tools-4

npm i -g azure-functions-core-tools@4

# Install ffmpeg for local testing
sudo apt-get install ffmpeg

npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

```
mkdir timelapse.functions
cd timelapse.functions

# Initialize the function app
func init --worker-runtime dotnet-isolated --target-framework net9.0
```

```
func new --name CreateTimelapse --template "HTTP trigger"
```

In timelapse.functions:
func host start

Attach to process in VSCode

