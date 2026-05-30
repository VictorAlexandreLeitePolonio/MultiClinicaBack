# MultiClinica API

Backend ASP.NET Core para um SaaS multi-clinica com PostgreSQL, autenticação por JWT em cookie httpOnly, isolamento por `ClinicaId`, painel operacional de SuperAdmin, billing comercial manual e anexos clínicos em storage externo.

## Stack

- .NET 10 / ASP.NET Core Controllers
- Entity Framework Core 10
- PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`
- JWT Bearer em cookie httpOnly
- BCrypt.Net para senha
- xUnit + WebApplicationFactory para testes de API

## Principais Módulos

- `api/auth`: login, cookie seguro por ambiente e `/api/auth/me`
- `api/superadmin/clinicas`: gestão global de clínicas, usuários, cobrança, desbloqueio e histórico comercial
- `api/patients`, `api/appointments`, `api/medicalrecords`, `api/payments`, `api/plans`, `api/financial`: módulos operacionais sempre escopados pela clínica autenticada
- `api/attachments`: metadados de anexos clínicos e envio para storage externo privado

## Ambiente Local

```bash
cp .env.example .env
docker compose up -d
dotnet restore
dotnet ef database update --project MultiClinica.API.csproj
dotnet run --project MultiClinica.API.csproj --launch-profile http
```

`DATABASE_URL` é a variável canônica para produção/Railway. O `appsettings.json` mantém uma connection string local equivalente para desenvolvimento.

## SuperAdmin Inicial

O bootstrap é idempotente e só roda quando todas as envs abaixo existem:

- `SUPER_ADMIN_NAME`
- `SUPER_ADMIN_EMAIL`
- `SUPER_ADMIN_PASSWORD`

Ele cria a clínica interna `Admin Interno` e o usuário `SuperAdmin`. Não há senha hardcoded de produção.

## Testes

```bash
dotnet test MultiClinica.API.sln
```

Os testes cobrem os pontos críticos iniciais: login, erros explícitos, isolamento por clínica, criação de clínica pelo SuperAdmin, bloqueio de criação indevida de SuperAdmin, desbloqueio manual com motivo e validação de anexos entre clínicas.
