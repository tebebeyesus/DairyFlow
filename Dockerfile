# DairyFlow — single image that builds the API + Blazor WASM front-end and serves both.
# (Targets .NET 7 to match the current projects. Bump the two tags below when you move to .NET 8 LTS.)

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DairyFlow.API/DairyFlow.API.csproj
RUN dotnet publish DairyFlow.API/DairyFlow.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Program.cs binds Kestrel to http://0.0.0.0:5000 inside the container.
EXPOSE 5000
ENTRYPOINT ["dotnet", "DairyFlow.API.dll"]
