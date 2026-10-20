$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project tests/VREC.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    dotnet run --project windows-tests/VREC.Windows.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows lifecycle tests failed' }
    dotnet publish VREC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item scripts/Install.ps1 artifacts/win-x64/Install.ps1
    Copy-Item README.md artifacts/win-x64/README.md
    Copy-Item docs artifacts/win-x64/docs -Recurse -Force
} finally { Pop-Location }
