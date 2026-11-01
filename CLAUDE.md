# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ProjetoLP.API is a **clinic management REST API** built with ASP.NET Core 10 / .NET 10 and SQLite. It handles patient scheduling, medical records, payments, and WhatsApp notifications for a physiotherapy clinic.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (dev server on http://localhost:5045, Swagger at /swagger)
dotnet run --launch-profile http

# EF Core migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

No test project exists — the API is tested via HTTP against the running server or Swagger UI.

## Architecture

### Stack
- **ASP.NET Core 10** with attribute-based routing controllers
- **Entity Framework Core 10 + SQLite** — database file `projetolp.db` is committed to the repo for local dev
- **JWT Bearer** stored in HttpOnly cookies (8-hour expiry)
- **BCrypt.Net-Next** for password hashing

### Startup Flow (`Program.cs`)
Program.cs does three things in order:
1. Registers all services (DI, EF, Auth, CORS, Swagger, typed HTTP client for WhatsApp)
2. Builds the middleware pipeline (CORS → StaticFiles → Auth/Authz → HTTPS → Controllers)
3. Seeds initial data on first run (2 users, 5 plans, sample patients/appointments/payments)

### Background Services (`/Services`)
- `AppointmentStatusUpdater` — auto-cancels past Scheduled appointments
- `PatientEmailOutboxWorker` — sends queued patient e-mails
- `Agent/AgentWorker` — processes the AI agent inbox (owner copilot on WhatsApp)
- `Transcription/ConsultationWorker` — processes recorded consultations

### Recorded consultations (transcription)
- Endpoints under `/api/appointments/{id}/consultation` (start, chunks, finish, report, review, retry, charge-suggestion, evolution) and `GET /api/patients/{patientId}/consultation-reports`. Spec: `docs/superpowers/specs/2026-11-01-consulta-transcricao-design.md`.
- `ConsultationProcessor` (single instance) interrupts recordings idle for 10 min, transcribes the concatenated chunks with `HttpTranscriber`, consumes `TranscriptionMinutes` (overage allowed) and asks `IChatModel` for the SOAP JSON. Failed reports retry 1/5 min and give up after 3 transcription failures; a failed AI report stays `Ready` with `ReportPending` and retries every 30 min.
- Audio lives in `Transcription:StoragePath` (default `App_Data/consultations`), outside `wwwroot`, and is deleted after a successful transcription or 7 days after `Failed`/`Interrupted`. Logs never include transcript, report or audio.
- Config: `Transcription__BaseUrl`, `__ApiKey`, `__Model`, `__StoragePath`; the SOAP report uses `Agent:Model`.

The owner copilot runs on the platform's WhatsApp number (Cloud API): webhook `GET|POST /webhooks/whatsapp` (verify token + `X-Hub-Signature-256`), owners link via `api/me/whatsapp-link` (one-time code sent as `conectar <code>`), each new 24h window consumes 1 `WhatsAppConversations`. Config: `WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__DisplayNumber`, `WhatsApp__AppSecret`, `WhatsApp__VerifyToken`. Reminders, patient chatbot (each clinic's own number via Embedded Signup) and transcription come in later specs.

### Platform plans and billing
- `Common/PlanMatrix.cs` is the single source of plans (Free / Pro / Premium): features, professional limits, monthly quotas, hub rank, prices and packs. Spec: `docs/superpowers/specs/2026-10-30-planos-plataforma-design.md`.
- `[RequireFeature(Features.X)]` gates endpoints (402 `PLAN_REQUIRED`); the plan is read from the DB, not the JWT. Jobs call `PlanMatrix.Has` directly.
- `ClinicPlanService` applies plan changes and deactivates professionals above the limit; `UsageService` consumes quotas atomically (`ClinicUsages`).
- Platform subscription runs on AbacatePay (`Services/Billing`, webhook `POST /api/billing/webhooks/abacatepay`, idempotent via `ProcessedWebhookEvents`). Product ids come from `AbacatePay__Products__<key>` env vars — keys never contain `:`.
- Existing clinics are founders (`IsFounder`, Premium forever). Self-service signup (`/api/public/signup`) creates Free clinics that only show in the hub after SuperAdmin approval (`IsHubApproved`).

### Online payments (Mercado Pago)
- Each clinic connects its own Mercado Pago account via OAuth + PKCE: `POST api/mercadopago/connect` returns the authorization URL, `GET api/mercadopago/callback` (`[AllowAnonymous]`, state is single-use, 10 min) stores the encrypted tokens and redirects to `{FRONTEND_URL}/app/configuracoes?mp=conectado|erro|conta-em-uso`. Settings (`GET status`, `PUT settings`, `DELETE`) require Administrador + `OnlinePayments`.
- Tokens are AES-GCM encrypted by `TokenCipher` (`MercadoPago__TokenEncryptionKey`); without the key connecting returns 503 `MERCADOPAGO_NOT_CONFIGURED`.
- `MercadoPagoTokenRefresher` runs every 24h (first pass after 1 min, outside `Testing`) and renews accounts expiring in < 30 days. MP 400/401/403 on refresh (or any clinic-token 401) marks the account `Revoked`.
- Paid charges arrive via webhook `POST /api/payments/webhooks/mercadopago` (HMAC `x-signature` with `MercadoPago__WebhookSecret`, always re-reads the payment from MP).
- Config: `MercadoPago__ClientId`, `__ClientSecret`, `__RedirectUri`, `__WebhookSecret`, `__TokenEncryptionKey` (32 bytes base64), optional `__ApiBaseUrl`/`__AuthBaseUrl`. Spec: `docs/superpowers/specs/2026-10-30-mercadopago-pagamento-online-design.md`.

### Data Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `User` | Email, CPF, Role (Admin/Fisio) | Password stored as BCrypt hash, never returned in responses |
| `Patient` | Phone, IsActive | IsActive=false blocks scheduling; soft-delete pattern |
| `Appointment` | Status (Scheduled/Completed/Cancelled), ReminderSent | Past scheduled → auto-cancelled hourly |
| `Payment` | Status (Pending/Paid/Cancelled), ReferenceMonth (YYYY-MM), PlanId | Amount auto-populated from linked Plan |
| `Plans` | TipoPlano (Mensal/Avulso), TipoSessao, Valor, IsActive | |
| `MedicalRecord` | PDF contract + image exams stored in `wwwroot/` with GUID names | |
| `Expense` | ReferenceMonth (YYYY-MM) | Used for monthly P&L balance |

### Authorization
- `[AllowAnonymous]`: `POST /api/auth/login` only
- `[Authorize]`: all other endpoints require valid JWT cookie
- `[Authorize(Roles = "Admin")]`: Payments write, Financial, Users management, Plans creation

### Response Patterns
- All list endpoints return `PagedResult<T>` with `{ data, page, pageSize, totalCount, totalPages }`
- Enums serialize as strings (`"Scheduled"` not `0`) via `JsonStringEnumConverter`
- File uploads: PDF max 10MB, images (JPG/PNG) max 5MB — stored in `wwwroot/`

### Business Rules (validated in controllers)
- Cannot schedule appointments for inactive patients
- Cannot delete patients with active appointments or payments
- Cannot delete plans that have associated payments
- Cannot delete the last Admin user
- Cannot delete Paid payments

## Configuration

`appsettings.json` contains JWT secret and Evolution API key in plaintext (development only). In production these must be moved to environment variables or user secrets.

```json
{
  "Jwt": { "Key": "...", "Issuer": "ProjetoLP.API", "Audience": "ProjetoLP.Client" },
  "EvolutionApi": { "BaseUrl": "http://localhost:8080", "ApiKey": "...", "Instance": "clinica" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=projetolp.db" }
}
```

CORS is hardcoded to `http://localhost:3000` (the frontend dev server).


 ---
  Plano de Ação Arquitetural

  Fase 1 — Fundação (Semana 1-2)

  Repository Pattern — desacopla queries do controller

  ProjetoLP.API/
  ├── Repositories/
  │   ├── Interfaces/
  │   │   ├── IPaymentRepository.cs
  │   │   ├── IPatientRepository.cs
  │   │   ├── IAppointmentRepository.cs
  │   │   └── ...
  │   ├── PaymentRepository.cs
  │   ├── PatientRepository.cs
  │   └── ...

  Começa pelos 2 controllers mais complexos: Payments e Patients. Os repositórios consolidam os Include() espalhados e encapsulam a paginação.

  ---
  Fase 2 — Negócio (Semana 2-3)

  Service Layer — tira a lógica dos controllers

  ProjetoLP.API/
  ├── Services/
  │   ├── Interfaces/
  │   │   ├── IPaymentService.cs
  │   │   └── IPatientService.cs
  │   ├── PaymentService.cs   ← regras de negócio + validações
  │   └── PatientService.cs

  Controllers ficam com ~10 linhas cada: recebem DTO → chamam service → mapeiam Result para HTTP.

  ---
  Fase 3 — Contratos de Erro (Semana 3)

  Result Pattern — substitui retornos misturados

  // Common/Result.cs
  public record Result<T>(bool IsSuccess, T? Value, string? ErrorCode, string? Message);

  // Common/ErrorCodes.cs
  public static class ErrorCodes
  {
      public const string NotFound       = "NOT_FOUND";
      public const string DuplicateMonth = "DUPLICATE_PAYMENT";
      public const string InactivePatient = "INACTIVE_PATIENT";
  }

  ---
  Fase 4 — Validação Centralizada (Semana 3-4)

  Validators — elimina o Regex repetido 6× e os if espalhados

  ProjetoLP.API/
  ├── Validators/
  │   ├── CreatePaymentDtoValidator.cs
  │   ├── CreateExpenseDtoValidator.cs
  │   └── ...

  ---
  Fase 5 — Background Services (Semana 4-5)

  Abstrações para os Jobs

  Services/
  ├── Interfaces/
  │   ├── IAppointmentReminderService.cs
  │   └── IMessageTemplateService.cs
  ├── AppointmentReminderService.cs   ← lógica extraída do Job
  ├── MessageTemplateService.cs       ← templates de WhatsApp
  └── Jobs/
      ├── AppointmentReminderJob.cs   ← só o loop de timer
      └── PaymentReminderJob.cs

  ---
  Ordem de execução recomendada

  Fase 1 → Fase 2 → Fase 3 → Fase 4 → Fase 5
    ↓         ↓        ↓
  Testes    Testes   Testes
  unitários unitários unitários
  ficam     ficam     ficam
  possíveis rápidos   completos

  Com Fase 1+2 completas, os 85 testes de integração atuais se tornam desnecessários — você testa o PaymentService diretamente com mocks, sem
  HTTP client.

  ---