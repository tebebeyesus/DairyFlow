# ============================================================
# DairyFlow - Project Setup Script
# Auto-detects your .NET version (.NET 6, 7, or 8)
# Usage: .\setup.ps1
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   DairyFlow - Project Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ── Check we're in the right folder ──────────────────────────
if (-not (Test-Path "DairyFlow.API") -or -not (Test-Path "DairyFlow.Core")) {
    Write-Host "ERROR: Please run this script from inside the DairyFlow-Clean folder." -ForegroundColor Red
    Write-Host "Example: cd C:\DairyFlow-Clean" -ForegroundColor Yellow
    Write-Host "         .\setup.ps1" -ForegroundColor Yellow
    exit 1
}

# ── Detect .NET version and set package versions ──────────────
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>&1
Write-Host "  Found: .NET SDK $dotnetVersion" -ForegroundColor Green

if ($dotnetVersion -match "^8\.") {
    $tfm           = "net8.0"
    $efVersion     = "8.0.0"
    $jwtVersion    = "8.0.0"
    $blazorVersion = "8.0.0"
    $swaggerVersion = "6.5.0"
} elseif ($dotnetVersion -match "^7\.") {
    $tfm           = "net7.0"
    $efVersion     = "7.0.0"
    $jwtVersion    = "7.0.0"
    $blazorVersion = "7.0.0"
    $swaggerVersion = "6.5.0"
} elseif ($dotnetVersion -match "^6\.") {
    $tfm           = "net6.0"
    $efVersion     = "6.0.0"
    $jwtVersion    = "6.0.0"
    $blazorVersion = "6.0.0"
    $swaggerVersion = "6.4.0"
} else {
    Write-Host "ERROR: Unsupported .NET version: $dotnetVersion" -ForegroundColor Red
    Write-Host "Install .NET 7 or 8 from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

Write-Host "  Targeting: $tfm" -ForegroundColor Green
Write-Host ""
Write-Host "Creating project files..." -ForegroundColor Yellow

# ── DairyFlow.Core.csproj ─────────────────────────────────────
Set-Content "DairyFlow.Core\DairyFlow.Core.csproj" -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
"@
Write-Host "  [OK] DairyFlow.Core.csproj" -ForegroundColor Green

# ── DairyFlow.Infrastructure.csproj ──────────────────────────
Set-Content "DairyFlow.Infrastructure\DairyFlow.Infrastructure.csproj" -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$efVersion" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="$efVersion" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="$efVersion" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="$jwtVersion" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DairyFlow.Core\DairyFlow.Core.csproj" />
  </ItemGroup>
</Project>
"@
Write-Host "  [OK] DairyFlow.Infrastructure.csproj" -ForegroundColor Green

# ── DairyFlow.API.csproj ──────────────────────────────────────
Set-Content "DairyFlow.API\DairyFlow.API.csproj" -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="$jwtVersion" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="$efVersion" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="$efVersion" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="$jwtVersion" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="$swaggerVersion" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="$jwtVersion" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DairyFlow.Core\DairyFlow.Core.csproj" />
    <ProjectReference Include="..\DairyFlow.Infrastructure\DairyFlow.Infrastructure.csproj" />
  </ItemGroup>
</Project>
"@
Write-Host "  [OK] DairyFlow.API.csproj" -ForegroundColor Green

# ── DairyFlow.Web.csproj ──────────────────────────────────────
Set-Content "DairyFlow.Web\DairyFlow.Web.csproj" -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="$blazorVersion" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="$blazorVersion" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="$efVersion" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DairyFlow.Core\DairyFlow.Core.csproj" />
  </ItemGroup>
</Project>
"@
Write-Host "  [OK] DairyFlow.Web.csproj" -ForegroundColor Green

# ── DairyFlow.sln ─────────────────────────────────────────────
Set-Content "DairyFlow.sln" -Encoding UTF8 -Value @'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DairyFlow.API", "DairyFlow.API\DairyFlow.API.csproj", "{A1B2C3D4-0001-0001-0001-000000000001}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DairyFlow.Core", "DairyFlow.Core\DairyFlow.Core.csproj", "{A1B2C3D4-0002-0002-0002-000000000002}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DairyFlow.Infrastructure", "DairyFlow.Infrastructure\DairyFlow.Infrastructure.csproj", "{A1B2C3D4-0003-0003-0003-000000000003}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DairyFlow.Web", "DairyFlow.Web\DairyFlow.Web.csproj", "{A1B2C3D4-0004-0004-0004-000000000004}"
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {A1B2C3D4-0001-0001-0001-000000000001}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A1B2C3D4-0001-0001-0001-000000000001}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {A1B2C3D4-0002-0002-0002-000000000002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A1B2C3D4-0002-0002-0002-000000000002}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {A1B2C3D4-0003-0003-0003-000000000003}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A1B2C3D4-0003-0003-0003-000000000003}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {A1B2C3D4-0004-0004-0004-000000000004}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A1B2C3D4-0004-0004-0004-000000000004}.Debug|Any CPU.Build.0 = Debug|Any CPU
  EndGlobalSection
EndGlobal
'@
Write-Host "  [OK] DairyFlow.sln" -ForegroundColor Green

# ── Restore packages ──────────────────────────────────────────
Write-Host ""
Write-Host "Restoring NuGet packages (may take a minute)..." -ForegroundColor Yellow
dotnet restore DairyFlow.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet restore failed." -ForegroundColor Red
    Write-Host "Check your internet connection and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host "  [OK] Packages restored" -ForegroundColor Green

# ── Done ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   Setup Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host ""
Write-Host "  1. Set up the database:" -ForegroundColor Yellow
Write-Host "     Open SSMS -> connect to localhost\SQLEXPRESS" -ForegroundColor White
Write-Host "     Open database\schema.sql and press F5 to run it" -ForegroundColor White
Write-Host ""
Write-Host "  2. Update connection string if needed:" -ForegroundColor Yellow
Write-Host "     Edit DairyFlow.API\appsettings.json" -ForegroundColor White
Write-Host "     Set Server=localhost\SQLEXPRESS" -ForegroundColor White
Write-Host ""
Write-Host "  3. Run the app:" -ForegroundColor Yellow
Write-Host "     cd DairyFlow.API" -ForegroundColor White
Write-Host "     dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "  4. Open in browser:" -ForegroundColor Yellow
Write-Host "     https://localhost:5000" -ForegroundColor White
Write-Host "     https://localhost:5000/api/docs  <- Swagger" -ForegroundColor White
Write-Host ""
