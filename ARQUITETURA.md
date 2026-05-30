# Arquitetura - MultiClinica API

O backend mantém Controllers, Services, Repositories e EF Core, agora com foco em SaaS multi-clinica.

## Regras Estruturais

- Banco compartilhado com isolamento por `ClinicaId`.
- Rotas operacionais usam a clínica do JWT; o frontend não envia `clinicaId` para trocar escopo.
- Rotas globais ficam separadas em `/api/superadmin/*`.
- Resources de outra clínica retornam `404` para usuários operacionais.
- Roles válidas: `SuperAdmin`, `Administrador`, `Profissional`, `Recepcao`.
- Soft delete usa `IsDeleted`/`DeletedAt`; inativação usa `IsActive`.
- Billing comercial de clínicas usa entidades separadas de pagamentos de pacientes.
- Arquivos clínicos ficam em storage externo privado; o banco guarda metadados e `ObjectKey`.

## Banco

PostgreSQL é o banco ativo. Use:

```bash
docker compose up -d
dotnet ef database update --project MultiClinica.API.csproj
```

Em produção, `DATABASE_URL` é a fonte canônica de conexão. Migrations devem ser executadas por comando/deploy step, não automaticamente no startup.
