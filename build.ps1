$ErrorActionPreference = "Stop"

dotnet tool restore
dotnet build

AddToPath ./extLauncher/bin/Debug/
