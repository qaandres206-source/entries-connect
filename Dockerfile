# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restaurar primero solo con los .csproj (mejor cache de capas)
COPY cwApp.sln ./
COPY cwApp/cwApp.csproj cwApp/
COPY cwApp.Client/cwApp.Client.csproj cwApp.Client/
RUN dotnet restore cwApp/cwApp.csproj

# Copiar el resto y publicar el proyecto servidor (incluye assets del cliente WASM)
COPY . .
RUN dotnet publish cwApp/cwApp.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# Render inyecta PORT; ASP.NET Core debe escuchar ahí en 0.0.0.0.
# Si no existe PORT (ejecución local), usa 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet cwApp.dll"]
