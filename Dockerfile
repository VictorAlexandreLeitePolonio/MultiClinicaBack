# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas o .csproj primeiro para aproveitar o cache de layers do Docker.
# Se as dependências não mudaram, o "dotnet restore" não é re-executado.
COPY ["MultiClinica.API.csproj", "."]
RUN dotnet restore MultiClinica.API.csproj

# Copia o restante do código e publica em modo Release.
COPY . .
RUN dotnet publish MultiClinica.API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copia o artefato publicado do stage de build.
COPY --from=build /app/publish .

# ASP.NET Core escuta na porta 8080 dentro do container por padrão.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MultiClinica.API.dll"]
