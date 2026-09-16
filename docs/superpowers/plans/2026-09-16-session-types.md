# Session Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed session-type enum with a clinic-scoped catalog consumed by plan creation, editing, filtering, and display.

**Architecture:** PostgreSQL stores session types and a required plan foreign key. Thin ASP.NET controllers call tenant-scoped services and repositories. The Next.js client uses a domain service, React Query hooks, and one reusable searchable field with a quick-create modal.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, PostgreSQL, xUnit, Next.js 16, React 19, TypeScript, React Query, React Hook Form, Zod, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-16-session-types-design.md`

## Global Constraints

- `SessionTypes` contains only `Id`, `Name`, and `ClinicaId`.
- Names are trimmed, required, at most 100 characters, and unique per clinic without case sensitivity.
- Only `GET /api/session-types?search=` and `POST /api/session-types` are added.
- The authenticated clinic scopes every lookup and mutation; the client never sends `ClinicaId`.
- Plans use `tipoSessaoId` and return `tipoSessaoId` plus `tipoSessaoName`.
- No edit, status, delete, management page, dependency installation, or unrelated refactor.

---

### Task 1: Persist and expose clinic session types

**Files:**
- Create: `Models/SessionType.cs`
- Create: `DTOs/SessionTypes/SessionTypeDtos.cs`
- Create: `Repositories/SessionTypeRepository.cs`
- Create: `Services/SessionTypeService.cs`
- Create: `Controllers/SessionTypesController.cs`
- Create: `tests/MultiClinica.Tests/SessionTypeTests.cs`
- Modify: `Data/AppDbContext.cs`
- Modify: `Program.cs`

**Interfaces:**
- Produces: `SessionTypeRepository.GetAllAsync(string? search)`, `GetByIdAsync(int id)`, `ExistsByNameAsync(string name)`, and `AddAsync(SessionType entity)`.
- Produces: `SessionTypeService.GetAllAsync(string? search)` and `CreateAsync(CreateSessionTypeDto dto)`.
- Produces: `SessionTypeResponseDto(int Id, string Name)` and `CreateSessionTypeDto.Name`.

- [ ] **Step 1: Write failing service tests**

Cover trimmed creation, empty and over-100 names, case-insensitive duplicate rejection, search, and tenant isolation using EF Core's in-memory provider and a stub `IUsuarioLogadoService`.

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `dotnet test tests/MultiClinica.Tests/MultiClinica.Tests.csproj --filter FullyQualifiedName~SessionTypeTests`

Expected: compilation fails because session-type classes do not exist.

- [ ] **Step 3: Implement the minimum backend catalog**

Use this entity shape:

```csharp
public class SessionType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;
}
```

The repository scopes every query with `usuario.ClinicaId`, uses `EF.Functions.ILike` for PostgreSQL search, orders by `Name`, and compares normalized names. The service trims before validation and maps only `Id` and `Name`. The controller uses `[Authorize(Roles = "Administrador")]`, returns `Ok` for GET, `Created` for POST, and `BadRequest` for validation or duplicates.

- [ ] **Step 4: Run focused tests**

Run the command from Step 2. Expected: PASS.

### Task 2: Replace the plan enum contract and create the migration

**Files:**
- Modify: `Models/Plans.cs`
- Modify: `DTOs/Plans/CreatePlanDto.cs`
- Modify: `DTOs/Plans/UpdatePlanDto.cs`
- Modify: `DTOs/Plans/PlanResponseDto.cs`
- Modify: `Controllers/PlansController.cs`
- Modify: `Services/PlanService.cs`
- Modify: `Services/Interfaces/IPlanService.cs`
- Modify: `Repositories/PlanRepository.cs`
- Modify: `Repositories/Interfaces/IPlanRepository.cs`
- Modify: `Data/AppDbContext.cs`
- Modify: `tests/MultiClinica.Tests/FinancialBalanceTests.cs`
- Create: `tests/MultiClinica.Tests/PlanSessionTypeTests.cs`
- Create: `Migrations/20260916190000_AddSessionTypes.cs` (EF may replace the timestamp with the generation time)
- Create: `Migrations/20260916190000_AddSessionTypes.Designer.cs` (EF may replace the timestamp with the generation time)
- Modify: `Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `SessionTypeRepository.GetByIdAsync(int id)` scoped to the authenticated clinic.
- Produces: `CreatePlanDto.TipoSessaoId`, `UpdatePlanDto.TipoSessaoId`, and response fields `TipoSessaoId` and `TipoSessaoName`.
- Produces: plan filter query parameter `tipoSessaoId`.

- [ ] **Step 1: Write failing plan tests**

Cover successful create/update, nonexistent ID, cross-tenant ID, response name, and filter by ID.

- [ ] **Step 2: Run focused plan tests and verify failure**

Run: `dotnet test tests/MultiClinica.Tests/MultiClinica.Tests.csproj --filter FullyQualifiedName~PlanSessionTypeTests`

- [ ] **Step 3: Implement the FK contract**

Replace the enum property with:

```csharp
public int TipoSessaoId { get; set; }
public SessionType TipoSessao { get; set; } = null!;
```

Include `TipoSessao` in plan queries. Validate the tenant-scoped ID before create and when update changes the ID. Map the name into every response.

- [ ] **Step 4: Generate and edit the migration**

Run: `dotnet ef migrations add AddSessionTypes`

Edit `Up` so it creates the table, inserts the clinic 2 and clinic 3 catalogs from the spec, maps clinic 2 plans from enum values `0..5`, verifies no null FK remains, makes the FK required, and drops the enum column. Create a unique PostgreSQL expression index over `"ClinicaId", lower(btrim("Name"))`.

- [ ] **Step 5: Validate migration and focused tests**

Run: `dotnet ef migrations script 20260826010326_AddProfessionalAvailability AddSessionTypes --idempotent`

Run the focused tests from Step 2 and `dotnet test tests/MultiClinica.Tests/MultiClinica.Tests.csproj`.

### Task 3: Add the frontend session-type field and quick-create flow

**Files:**
- Create: `src/app/(authenticated)/app/planos/services/session-types.service.ts`
- Create: `src/app/(authenticated)/app/planos/hooks/useSessionTypes.ts`
- Create: `src/app/(authenticated)/app/planos/components/SessionTypeField.tsx`
- Create: `src/app/(authenticated)/app/planos/components/SessionTypeCreateDialog.tsx`
- Create: `src/app/(authenticated)/app/planos/components/SessionTypeField.test.tsx`
- Modify: `src/app/(authenticated)/app/planos/components/PlanoRegister.tsx`
- Modify: `src/app/(authenticated)/app/planos/components/PlanoDetails.tsx`
- Modify: `src/app/(authenticated)/app/planos/components/PlanoList.tsx`
- Modify: `src/app/(authenticated)/app/planos/schemas/plano.schema.ts`
- Modify: `src/app/(authenticated)/app/planos/services/plans.service.ts`
- Modify: `src/types/index.ts`

**Interfaces:**
- Consumes: `GET /api/session-types?search=` and `POST /api/session-types`.
- Produces: `SessionTypeField` props `value`, `onChange`, `disabled`, and `error`.
- Produces: `PlanoFormData.tipoSessaoId: number`.

- [ ] **Step 1: Write the failing component test**

Verify loading and empty states, selecting a searched result, opening quick create, preserving the parent form, and selecting the returned type.

- [ ] **Step 2: Run the focused test and verify failure**

Run: `npm test -- 'src/app/(authenticated)/app/planos/components/SessionTypeField.test.tsx'`

- [ ] **Step 3: Implement the service and hooks**

Use React Query with query keys `['session-types', search]`. The mutation invalidates `['session-types']` and returns the created item so the component can select its `id` immediately.

- [ ] **Step 4: Implement the reusable field and modal**

Use existing `Button`, `FormField`, and Radix Dialog patterns. Render a search input and a bounded scrollable result list; do not add a dependency. Keep the modal inside the mounted form component and use `type="button"` for non-submit actions.

- [ ] **Step 5: Replace fixed plan fields and contracts**

Set new-plan `tipoSessaoId` to `0`, validate with `z.number().int().positive("Tipo de sessão é obrigatório")`, populate edit state from `data.tipoSessaoId`, and render `tipoSessaoName` in the list.

- [ ] **Step 6: Run frontend checks**

Run: `npm test -- 'src/app/(authenticated)/app/planos/components/SessionTypeField.test.tsx'`, `npm test`, `npm run lint`, and `npm run build`.

### Task 4: Integrated verification, production migration handoff, and commits

**Files:**
- Verify all changed files in both repositories.

**Interfaces:**
- Consumes: completed backend and frontend contracts.
- Produces: two clean, reviewable commits on each repository's `main` branch and a production migration command if direct production access is not unequivocally configured.

- [ ] **Step 1: Verify repository state**

Run `git status --short` and `git diff --check` in both repositories. Confirm no unrelated file changed.

- [ ] **Step 2: Run the software quality gate**

Read and execute `/Users/victorpolonio123/.codex/skills/software-quality-gate/SKILL.md` separately for backend and frontend. Keep all scanner tooling outside both repositories and compare git status before and after.

- [ ] **Step 3: Inspect production deployment configuration**

If a unique production connection is available, back up and apply `dotnet ef database update` with the explicit production environment. Otherwise, return the exact command with the required environment variable placeholder; never guess a production target.

- [ ] **Step 4: Commit backend and frontend**

Commit only task files on `main`, using concise conventional commit messages. Confirm both repositories are clean and report commit hashes plus validation results.
