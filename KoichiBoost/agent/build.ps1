# Cria a solution (se necessário), restaura, compila e roda os testes.
# Uso (PowerShell, na pasta agent\):  .\build.ps1
$ErrorActionPreference = "Stop"
if (-not (Test-Path "KoichiBoost.sln")) {
    dotnet new sln -n KoichiBoost | Out-Null
    dotnet sln add src/KoichiBoost.Core src/KoichiBoost.Windows src/KoichiBoost.Agent tests/KoichiBoost.Core.Tests
}
dotnet restore
dotnet build -c Release --no-restore
dotnet test tests/KoichiBoost.Core.Tests -c Release --no-build
Write-Host "`nOK. Execute:  dotnet run --project src/KoichiBoost.Agent -c Release"
