param([switch]$Publish,[switch]$Test)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.tools\home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:NUGET_PACKAGES=Join-Path $PSScriptRoot '.tools\packages'
$dotnet=Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if(!(Test-Path -LiteralPath $dotnet)){$dotnet='dotnet'}
& $dotnet build src\TwistTidy\TwistTidy.csproj -c Release -v minimal
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
if($Test){& $dotnet run --project tests\TwistTidy.Tests -c Release;if($LASTEXITCODE -ne 0){throw 'Tests failed.'}}
if($Publish){& $dotnet publish src\TwistTidy\TwistTidy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o dist\TwistTidy --source https://api.nuget.org/v3/index.json;if($LASTEXITCODE -ne 0){throw 'Publish failed.'};Copy-Item -LiteralPath dist\TwistTidy\TwistTidy.exe -Destination dist\TwistTidySetup.exe -Force}
if($Publish){Copy-Item -Path licenses\*.txt -Destination dist\TwistTidy -Force}
