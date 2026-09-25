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
- `api/attachments`: metadados de anexos clínicos e envio para bucket S3 privado

## Ambiente Local

```bash
cp .env.example .env
docker compose up -d
dotnet restore
set -a
source .env
set +a
dotnet ef database update --project MultiClinica.API.csproj
dotnet run --project MultiClinica.API.csproj --launch-profile http
```

`DATABASE_URL` é a variável canônica para produção/Railway. O `appsettings.json` mantém uma connection string local equivalente para desenvolvimento.

## Storage S3

Os anexos clínicos usam bucket S3 privado. O backend salva o `ObjectKey` no banco e gera URL temporária para download em `/api/attachments/{id}/download`; não deixe o bucket público.

Variáveis obrigatórias:

- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_REGION`
- `S3_BUCKET_NAME`

## Importação de pacientes

`POST /api/patients/import` recebe uma requisição `multipart/form-data` com o arquivo no campo `file` e um UUID no cabeçalho `Idempotency-Key`. A rota exige autenticação de clínica e aceita os papéis `Administrador`, `Profissional` e `Recepcao`.

São aceitos arquivos `.csv` em UTF-8 (com ou sem BOM, delimitados por vírgula ou ponto e vírgula) e `.xlsx` com uma planilha de dados. O arquivo pode ter até 10 MB e 10.000 linhas; os limites podem ser alterados por `PatientImport__MaxFileBytes` e `PatientImport__MaxRows`. Colunas aceitas, em qualquer ordem e sem diferenciar maiúsculas de minúsculas: `Name`, `Email`, `CPF`, `Rg`, `BirthDate`, `Rua`, `Numero`, `Bairro`, `Cidade`, `Estado`, `Cep` e `Phone`. `Name` é obrigatória. `BirthDate` é opcional e aceita `DD/MM/AAAA`, `AAAA-MM-DD` ou uma célula de data nativa do Excel; datas inválidas ou futuras rejeitam apenas a linha. Por exemplo, um CSV mínimo é:

```csv
Name
Maria da Silva
João Souza
```

Linhas vazias são ignoradas. Erros de validação são retornados por linha, enquanto as linhas válidas são persistidas; cabeçalhos ou estrutura inválidos rejeitam o arquivo inteiro antes de qualquer inserção. Um paciente sem e-mail não recebe conta de portal nem notificação. Com e-mail, a nova conta pendente ou o vínculo com uma conta existente entram na mesma transação da importação, sem sobrescrever os dados da identidade existente; a fila persistente envia a notificação após o commit.

O relatório de sucesso contém `importId`, `status`, `totalRows`, `importedCount`, `rejectedCount`, `emailsQueuedCount`, `emailsSkippedNoEmailCount` e `results`. Cada item de `results` informa o número original da linha, o resultado e eventuais erros. Reenviar o mesmo arquivo com a mesma chave devolve o resultado original; uma chave já usada com conteúdo diferente retorna `409 Conflict`. Gere uma nova chave ao iniciar outra importação. Os parâmetros da fila estão disponíveis em `.env.example`.

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
