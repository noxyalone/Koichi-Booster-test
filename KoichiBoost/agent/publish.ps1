# Gera o executável do Koichi Boost (Windows x64), autocontido: não exige .NET instalado.
# Uso (PowerShell, na pasta agent\):  .\publish.ps1            -> arquivo único
#                                      .\publish.ps1 -Folder   -> pasta (use se --sensors falhar no arquivo único)
param([switch]$Folder)
$ErrorActionPreference = "Stop"
$out = "dist"
$args = @("publish", "src/KoichiBoost.Agent", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-o", $out)
if (-not $Folder) { $args += @("-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true") }
dotnet @args
Write-Host "`nPronto: $out\koichi-agent.exe"
Write-Host "Teste:  .\$out\koichi-agent.exe --help"
