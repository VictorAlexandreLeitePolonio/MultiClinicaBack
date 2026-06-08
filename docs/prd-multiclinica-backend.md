# PRD - MultiClinica Backend

## Problem Statement

O backend atual nasceu como `ProjetoLP.API`, uma API para uma unica clinica com SQLite, roles simples, uploads locais e integracao com WhatsApp/Evolution API. Esse modelo precisa evoluir para um produto multi-clinica, sem alterar o projeto original da clinica da mae do Victor.

O novo backend precisa ter repositorio proprio, historico limpo, PostgreSQL, isolamento forte por clinica, painel SuperAdmin operacional, controle manual/comercial de pagamentos das clinicas, storage externo para arquivos clinicos e testes automatizados nos pontos criticos de seguranca e isolamento.

## Solution

Criar o novo backend `MultiClinica.API` em um repositorio privado `MultiClinicaBack`, mantendo a stack .NET/ASP.NET Core atual e migrando apenas o banco para PostgreSQL.

O sistema sera multi-clinica com banco unico compartilhado. Cada usuario pertence a uma unica clinica. Dados operacionais sempre terao `ClinicaId`, e o backend filtrara os dados pela clinica do usuario autenticado. O frontend nao podera enviar `ClinicaId` livre em rotas operacionais.

O SuperAdmin tera um painel global para criar e administrar clinicas, usuarios, cobrancas, pagamentos, bloqueios financeiros e historico comercial. O produto nao tera billing automatico, trial publico ou onboarding publico nesta fase.

## User Stories

1. As a SuperAdmin, I want to create a clinic manually, so that I can onboard selected customers without public signup.
2. As a SuperAdmin, I want to list all clinics, so that I can manage the whole customer base.
3. As a SuperAdmin, I want to filter clinics by active, inactive, paid, overdue and blocked status, so that I can quickly identify operational issues.
4. As a SuperAdmin, I want to view clinic details, so that I can inspect cadastral, operational and billing data in one place.
5. As a SuperAdmin, I want to edit clinic cadastral data, so that customer records stay up to date.
6. As a SuperAdmin, I want to activate or deactivate a clinic manually, so that I can control access when needed.
7. As a SuperAdmin, I want to configure monthly billing for a clinic, so that expected charges can be generated automatically.
8. As a SuperAdmin, I want clinics to start with billing disabled, so that I do not generate incorrect charges before configuration.
9. As a SuperAdmin, I want monthly charges to be created automatically, so that I do not need to create every billing period manually.
10. As a SuperAdmin, I want to register clinic payments manually, so that I can control commercial status without payment gateway integration.
11. As a SuperAdmin, I want to cancel or adjust clinic charges, so that I can correct operational mistakes.
12. As a SuperAdmin, I want overdue clinics to be blocked automatically, so that unpaid clinics cannot keep using the system.
13. As a SuperAdmin, I want to manually unblock a clinic with a required reason, so that exceptions are explicit and audited.
14. As a SuperAdmin, I want a commercial history timeline per clinic, so that I can understand what happened with each customer.
15. As a SuperAdmin, I want to create the first administrator for a clinic, so that the clinic can start operating.
16. As a SuperAdmin, I want to create users in any clinic, so that I can support customers globally.
17. As a SuperAdmin, I want global routes separated from operational routes, so that customer-facing endpoints cannot accidentally switch clinic scope.
18. As an Administrator, I want to create users only in my clinic, so that I can manage my local team.
19. As an Administrator, I want to create Professional and Recepcao users, so that I can delegate clinical and front-desk work.
20. As an Administrator, I want to be prevented from creating SuperAdmin users, so that global access remains protected.
21. As a clinic user, I want login failures to explain inactive user, inactive clinic or billing block when credentials are correct, so that I know what action is needed.
22. As a clinic user, I want invalid email/password errors to stay generic, so that account enumeration is reduced.
23. As a clinic user, I want to access only data from my clinic, so that patient and financial data remains isolated.
24. As a clinic user, I want records from other clinics to return 404, so that cross-clinic resource existence is not leaked.
25. As a clinic user, I want my session to use a secure httpOnly cookie, so that the token is not exposed to frontend JavaScript.
26. As a developer, I want the JWT to include user id, role and clinic id, so that backend services can scope operations consistently.
27. As a developer, I want a centralized logged-user service, so that services do not duplicate claim parsing.
28. As a developer, I want explicit `ClinicaId` filters in repositories and services, so that isolation is easy to review.
29. As a developer, I want PostgreSQL in local development, so that migrations and queries match production behavior.
30. As a developer, I want `DATABASE_URL` as the connection string standard, so that Railway deployment is straightforward.
31. As a developer, I want migrations to run by explicit command, so that production startup does not mutate schema unexpectedly.
32. As a developer, I want the initial SuperAdmin bootstrap to use env vars, so that no production password is hardcoded.
33. As a developer, I want Swagger public only in development, so that production does not expose API documentation unnecessarily.
34. As a developer, I want the project renamed to `MultiClinica.API`, so that the new product is not confused with `MultiClinica`.
35. As a developer, I want a new test project, so that tests reflect the multi-clinic domain instead of the old single-clinic app.
36. As a professional, I want appointments to choose a professional from the same clinic, so that scheduling remains scoped and correct.
37. As a professional, I want medical records to be scoped to my clinic, so that clinical data cannot cross tenants.
38. As a front-desk user, I want patient creation to use my clinic automatically, so that I do not need to choose a clinic manually.
39. As a clinic user, I want files attached to patients and records to be private, so that clinical documents are protected.
40. As a clinic user, I want patient attachments and record attachments separated logically, so that documents can be found in the right context.
41. As a developer, I want storage in AWS S3, so that deploys do not depend on local container filesystem.
42. As a developer, I want the database to store file metadata and object keys only, so that large file bytes stay outside PostgreSQL.
43. As a developer, I want uploads to validate clinic ownership of patient and record, so that files cannot be attached across clinics.
44. As a system owner, I want WhatsApp/Evolution API removed from the initial product, so that deployment stays simpler.
45. As a system owner, I want automatic reminder fields removed, so that unused notification concepts do not leak into the API contract.
46. As a system owner, I want soft delete and audit fields, so that clinical and commercial history is preserved.
47. As a system owner, I want explicit failures, so that operational problems are easier to diagnose.

## Implementation Decisions

- Create a new private GitHub repository named `MultiClinicaBack` with clean history.
- Use the current backend as the starting point, but remove the original remote that points to `MultiClinicaBack`.
- Keep .NET/ASP.NET Core, Controllers, Services, Repositories, DTOs and EF Core.
- Do not rewrite to Node.js/Fastify in this phase.
- Rename public and internal identity from `MultiClinica.API` to `MultiClinica.API`, including solution/project names, root namespace, README, Swagger title, config names and seeds.
- Migrate from SQLite to PostgreSQL using `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Remove SQLite as the active database provider.
- Use `DATABASE_URL` as the canonical connection string variable.
- Add Docker Compose with PostgreSQL for local development.
- Run migrations by explicit command or deploy step. Do not run production migrations automatically in API startup.
- Start with an empty PostgreSQL database.
- Seed only the internal Admin clinic and the Victor SuperAdmin user.
- Bootstrap SuperAdmin through an idempotent command or routine using `SUPER_ADMIN_NAME`, `SUPER_ADMIN_EMAIL` and `SUPER_ADMIN_PASSWORD`.
- Do not hardcode production admin credentials.
- Use a single shared database with clinic isolation through `ClinicaId`.
- Use `int` IDs for `ClinicaId`, following the existing entity ID style.
- Create `Clinica` with `Nome` as razao social, `NomeFantasia`, `NomeResponsavel`, `Cnpj`, `Email`, `Telefone`, address fields, active status, billing settings and audit fields.
- Do not implement algorithmic CNPJ/CPF/phone/CEP validation in this phase.
- Normalize CPF, CNPJ, phone and CEP to digits before saving.
- Add `ClinicaId` to all operational entities: users, patients, appointments, medical records, payments, plans, expenses and attachments.
- Remove the WhatsApp module from the initial product, including Evolution API configuration, WhatsApp logs, WhatsApp services and reminder jobs.
- Remove reminder fields such as appointment reminder sent and payment reminder sent.
- Define roles as `SuperAdmin`, `Administrador`, `Profissional` and `Recepcao`.
- Remove or migrate old `Admin` and `Fisio` role usage.
- Keep JWT in httpOnly cookie.
- JWT claims must include user id, email, role and clinic id.
- Cookie configuration follows environment rules: development may avoid Secure for localhost, production must use Secure.
- Use CORS through `CORS_ALLOWED_ORIGINS`, supporting comma-separated origins and credentials.
- Create `UsuarioLogadoService` to expose `UserId`, `ClinicaId`, `Role` and `IsSuperAdmin`.
- Controllers should not parse claims manually.
- Use explicit clinic-scoped repository/service methods instead of EF Core global query filters in the first phase.
- Operational routes must not accept `clinicaId` query params to change scope.
- SuperAdmin global routes must be separated from regular operational routes.
- Out-of-scope resources from another clinic must return 404 for clinic users.
- Use 403 for real permission failures on known routes.
- Login must use explicit failures when credentials are correct but user, clinic or billing status blocks access.
- Invalid email/password remains a generic credential error.
- Separate action executor from selected professional/responsible user.
- Remove free `UserId` from DTOs when it represents the authenticated creator/executor.
- Use semantic fields such as `ProfessionalId` or `ResponsavelId` when the frontend chooses a professional.
- Validate selected professionals belong to the same clinic.
- Implement soft delete/inactivation as a standard pattern.
- Use a small auditable base class for shared audit fields.
- Use `IsDeleted` and `IsActive` with different meanings.
- Preserve clinical and financial history; avoid physical deletion in common flows.
- Include `CreatedAt`, `UpdatedAt`, `DeletedAt`, `CreatedByUserId`, `UpdatedByUserId`, `DeletedByUserId` and deletion/active markers where appropriate.
- Use `UsuarioLogadoService.UserId` for audit author fields.
- Create a SuperAdmin clinic management module with routes for clinics, clinic users, billing records and commercial history.
- SuperAdmin can create, list, edit, activate and deactivate clinics.
- SuperAdmin can create users in any clinic.
- Administrator can create only Professional and Recepcao users in their own clinic.
- Create a separate clinic billing/payment entity, distinct from patient payments.
- Clinic billing is manual/commercial, not automatic gateway billing.
- Add clinic billing config fields: `ValorMensalidade`, `DiaVencimento`, `CobrancaAtiva` and `DataInicioCobranca`.
- New clinics start active but with billing disabled.
- A job creates monthly pending charges automatically for clinics with billing enabled.
- A daily job checks overdue unpaid clinic charges and sets `IsBlockedByBilling`.
- Login/access also verifies billing block so access is protected even if the job fails.
- Use `IsBlockedByBilling` separately from `IsActive`.
- Login blocks if clinic is inactive or billing-blocked.
- SuperAdmin can manually unblock billing block with required reason and audit record.
- Create a commercial event history/timeline per clinic.
- Commercial history should record clinic creation, billing config changes, charge creation, payment registration, payment cancellation, automatic block, manual unblock, clinic activation/inactivation and admin user creation.
- Use AWS S3 as private external storage for clinical files.
- Do not store file bytes in PostgreSQL.
- Store file metadata and object key in the database.
- Create a clinical attachment entity.
- Attachments have `ClinicaId`, required `PatientId` and optional `MedicalRecordId`.
- Attachments store type, original filename, object key, content type, size, uploaded by user and uploaded at.
- Backend must validate patient, medical record and attachment belong to the same clinic.
- Access files through backend-controlled download or temporary signed URL. Do not expose the S3 bucket publicly.
- Remove the old `Contrato` and `ExamesImagem` string-field approach gradually in favor of attachments.
- Swagger is enabled in development and disabled or protected in production. First phase preference is disabled in production.

## Testing Decisions

- Create a new test project for `MultiClinica.API`.
- Do not depend on the old missing `MultiClinica.Tests` project.
- Fix the solution so it references only existing projects.
- Tests should focus on external API behavior and domain outcomes, not implementation details.
- Prioritize integration/API tests for authentication, authorization and clinic isolation.
- Cover login with inactive user.
- Cover login with inactive clinic.
- Cover login/access with billing-blocked clinic.
- Cover generic error for invalid credentials.
- Cover 404 when a clinic user requests a resource from another clinic.
- Cover patient creation always using the authenticated user's clinic.
- Cover Administrator being unable to create SuperAdmin.
- Cover Administrator being unable to create users in another clinic.
- Cover SuperAdmin creating a clinic and first Administrator.
- Cover operational listing scoped by `ClinicaId`.
- Cover selected professional validation against the same clinic.
- Cover clinic charge generation behavior.
- Cover overdue charge blocking behavior.
- Cover manual unblock requiring reason.
- Cover attachment upload validating patient/medical record clinic ownership.
- Test S3 storage through a fake/mock storage abstraction rather than real AWS calls.
- Prefer PostgreSQL for integration tests when viable, using isolated database/container strategy.

## Out of Scope

- Frontend implementation.
- Public clinic signup/onboarding.
- Billing gateway integration.
- Stripe or other payment provider integration.
- Trial automation.
- Automatic subscription management.
- Full CPF/CNPJ/phone/CEP validation.
- WhatsApp/Evolution API integration.
- Appointment/payment reminder notifications.
- Rewriting backend to Node.js/Fastify.
- Migrating existing SQLite data from the old project.
- Importing the original clinic's production data.
- Public S3 bucket access.
- Broad redesign of unrelated business rules.

## Further Notes

- The original project for the clinic owner's mother must remain unchanged in the old repository.
- The new repository should start clean and private.
- The current backend project builds directly with warnings, but the solution references a missing test project and must be cleaned up.
- Current nullable warnings should be tracked as cleanup risk, especially around services and repositories touched by multi-clinic work.
- The product should fail explicitly where safe, especially for operational and billing states.
- For cross-clinic resources, explicit failure means returning 404 rather than leaking existence.
