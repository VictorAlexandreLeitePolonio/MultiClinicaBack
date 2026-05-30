# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas o .csproj primeiro para aproveitar o cache de layers do Docker.
# Se as dependências não mudaram, o "dotnet restore" não é re-executado.
COPY ["ProjetoLP.API.csproj", "."]
RUN dotnet restore ProjetoLP.API.csproj

# Copia o restante do código e publica em modo Release.
COPY . .
RUN dotnet publish ProjetoLP.API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copia o artefato publicado do stage de build.
COPY --from=build /app/publish .

# Cria os diretórios para dados persistentes (banco + uploads).
# Em produção esses diretórios são sobrescritos por volumes Docker.
RUN mkdir -p /app/data /app/wwwroot

# ASP.NET Core escuta na porta 8080 dentro do container por padrão.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ProjetoLP.API.dll"]
