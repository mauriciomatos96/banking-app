# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so this layer is cached until a project file changes.
COPY src/BankingApi.Api/BankingApi.Api.csproj src/BankingApi.Api/
RUN dotnet restore src/BankingApi.Api/BankingApi.Api.csproj

COPY src/ src/
RUN dotnet publish src/BankingApi.Api/BankingApi.Api.csproj -c Release -o /app --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

# The aspnet image listens on 8080 and runs as the non-root "app" user.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "BankingApi.Api.dll"]
