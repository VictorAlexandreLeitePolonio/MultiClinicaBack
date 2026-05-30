# 📚 Guia de Arquitetura - ProjetoLP.API

> **Objetivo deste guia:** Explicar de forma didática a estrutura de pastas e a responsabilidade de cada camada da arquitetura, para que você possa entender e navegar no projeto sem se perder.

---

## 🎯 Visão Geral da Arquitetura

Este projeto segue o padrão **Repository + Service Layer + Result Pattern**, que separa as responsabilidades em camadas bem definidas:

```
┌─────────────────────────────────────────────────────────────┐
│                    📱 CLIENTE (Frontend)                     │
└────────────────────┬────────────────────────────────────────┘
                     │ HTTP/HTTPS
┌────────────────────▼────────────────────────────────────────┐
│  🎮 CONTROLLERS (Camada de Apresentação)                     │
│  • Recebe requisições HTTP                                   │
│  • Valida entrada básica                                     │
│  • Chama os Services                                         │
│  • Retorna respostas HTTP (200, 404, 500, etc)              │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  ⚙️ SERVICES (Camada de Negócio)                             │
│  • Contém TODA a lógica de negócio                         │
│  • Valida regras de domínio                                │
│  • Usa o Result<T> para retornar sucesso/erro              │
│  • Chama os Repositories para acessar dados                │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  🗄️ REPOSITORIES (Camada de Acesso a Dados)                 │
│  • Faz queries no banco de dados (EF Core)                 │
│  • NÃO tem lógica de negócio                               │
│  • Apenas busca, salva, atualiza e deleta                  │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  💾 BANCO DE DADOS (SQLite)                                  │
│  • Dados persistidos em projetolp.db                       │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 Estrutura de Pastas Explicada

### 1. 📂 `Common/` - Fundamentos Compartilhados

**Responsabilidade:** Contém classes e utilitários usados por TODAS as camadas do projeto.

| Arquivo | O que faz |
|---------|-----------|
| `Result.cs` | Implementa o **Result Pattern** - uma classe genérica que encapsula o resultado de uma operação (sucesso ou falha) |
| `ErrorCodes.cs` | Constantes com códigos de erro padronizados (NOT_FOUND, DUPLICATE_EMAIL, etc) |

**🔍 Exemplo prático:**
```csharp
// Em vez de retornar null ou lançar exceção:
public Result<Payment> CreatePayment(CreatePaymentDto dto)
{
    if (!patient.IsActive)
        return Result<Payment>.Fail(ErrorCodes.InactivePatient, "Paciente inativo");
    
    // ... cria o pagamento
    return Result<Payment>.Ok(payment);
}
```

**💡 Por que usar?** Padroniza como o sistema comunica erros, evitando exceções descontroladas e tornando o código mais previsível.

---

### 2. 📂 `Controllers/` - Porta de Entrada da API

**Responsabilidade:** Receber requisições HTTP, chamar os Services e retornar respostas HTTP adequadas.

**🎯 Características:**
- São **"thin"** (magros) - têm pouquíssima lógica
- Usam atributos como `[HttpGet]`, `[HttpPost]`, `[Authorize]`
- Convertem `Result<T>` em códigos HTTP (200, 404, 400, etc)
- NUNCA acessam o banco diretamente

**📋 Estrutura típica de um Controller:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet]           // GET /api/payments
    public async Task<IActionResult> GetAll() { ... }
    
    [HttpGet("{id}")]   // GET /api/payments/123
    public async Task<IActionResult> GetById(int id) { ... }
    
    [HttpPost]          // POST /api/payments
    public async Task<IActionResult> Create(CreatePaymentDto dto) { ... }
    
    [HttpPut("{id}")]   // PUT /api/payments/123
    public async Task<IActionResult> Update(int id, UpdatePaymentDto dto) { ... }
    
    [HttpDelete("{id}")] // DELETE /api/payments/123
    public async Task<IActionResult> Delete(int id) { ... }
}
```

**⚠️ Regra de Ouro:** Se você encontrar um `DbContext` dentro de um Controller, algo está errado! Controllers devem chamar apenas Services.

---

### 3. 📂 `DTOs/` - Data Transfer Objects

**Responsabilidade:** Definir o formato dos dados que entram e saem da API.

**🎯 Por que DTOs?**
- **Segurança:** Nunca exponha suas entidades do banco diretamente
- **Flexibilidade:** O DTO pode ter campos diferentes da entidade
- **Validação:** DTOs definem o "contrato" da API

**📋 Organização interna:**
```
DTOs/
├── Payment/
│   ├── CreatePaymentDto.cs      → Dados para CRIAR pagamento
│   ├── UpdatePaymentDto.cs      → Dados para ATUALIZAR pagamento
│   └── PaymentResponseDto.cs    → Dados de RESPOSTA da API
├── Patient/
│   ├── CreatePatientDto.cs
│   ├── UpdatePatientDto.cs
│   ├── PatientResponseDto.cs
│   └── PatientProfileDto.cs     → DTO especial (perfil completo)
├── PagedResult.cs               → Wrapper para listas paginadas
└── ... outros módulos
```

**🔍 Exemplo:**
```csharp
// Entidade (banco de dados) - NÃO vai para o cliente
public class Payment {
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; }  // Navigation property
    public decimal Amount { get; set; }
}

// DTO de Resposta - VAI para o cliente
public class PaymentResponseDto {
    public int Id { get; set; }
    public string PatientName { get; set; }  // Só o nome, não o objeto inteiro!
    public decimal Amount { get; set; }
}
```

---

### 4. 📂 `Models/` - Entidades do Banco de Dados

**Responsabilidade:** Representar as tabelas do banco de dados.

**🎯 Características:**
- Cada classe = uma tabela no SQLite
- Propriedades = colunas da tabela
- Navigation properties = relacionamentos (FK)

**📋 Exemplo de entidade:**
```csharp
public class Payment
{
    public int Id { get; set; }                    // PK
    public int PatientId { get; set; }             // FK
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }      // Enum
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties (não são colunas)
    public Patient Patient { get; set; } = null!;
    public Plans Plan { get; set; } = null!;
}

public enum PaymentStatus {
    Pending,    // 0
    Paid,       // 1
    Cancelled   // 2
}
```

**⚠️ Importante:**
- Models NÃO devem ter lógica de negócio
- São classes POCO (Plain Old CLR Objects) - apenas dados

---

### 5. 📂 `Repositories/` - Acesso a Dados

**Responsabilidade:** Isolar todas as operações de banco de dados.

**📁 Estrutura:**
```
Repositories/
├── Interfaces/
│   ├── IPaymentRepository.cs
│   ├── IPatientRepository.cs
│   └── ... (contratos)
├── PaymentRepository.cs
├── PatientRepository.cs
└── ... (implementações)
```

**🎯 Princípio:**
> "Um Repository por agregado de domínio"

**📋 Métodos típicos de um Repository:**
| Método | Função |
|--------|--------|
| `GetByIdAsync(id)` | Busca uma entidade pelo ID |
| `GetPagedAsync(...)` | Busca paginada com filtros |
| `AddAsync(entity)` | Insere nova entidade |
| `SaveChangesAsync()` | Salva alterações |
| `DeleteAsync(entity)` | Remove entidade |
| `ExistsAsync(...)` | Verifica se existe |

**🔍 Exemplo:**
```csharp
public class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await db.Payments
            .Include(p => p.Patient)  // JOIN com Patients
            .Include(p => p.Plan)     // JOIN com Plans
            .FirstOrDefaultAsync(p => p.Id == id);
    }
    
    public async Task<bool> ExistsAsync(int patientId, string month)
    {
        return await db.Payments.AnyAsync(p => 
            p.PatientId == patientId && p.ReferenceMonth == month);
    }
}
```

**💡 Por que usar Repository?**
1. **Testabilidade:** Você pode mockar o Repository nos testes
2. **Manutenibilidade:** Se mudar o banco, muda só aqui
3. **Legibilidade:** Query complexa fica isolada

---

### 6. 📂 `Services/` - Lógica de Negócio

**Responsabilidade:** Implementar TODAS as regras de negócio do sistema.

**📁 Estrutura:**
```
Services/
├── Interfaces/
│   ├── IPaymentService.cs
│   ├── IPatientService.cs
│   └── ...
├── PaymentService.cs
├── PatientService.cs
├── AppointmentStatusUpdater.cs    → Background job
├── AppointmentReminderJob.cs      → Background job
└── PaymentReminderJob.cs          → Background job
```

**🎯 Características:**
- Contém validações de negócio
- Usa `Result<T>` para retornos
- Chama Repositories para persistir
- NÃO acessa banco diretamente (sempre via Repository)

**📋 Exemplo de Service:**
```csharp
public class PaymentService(IPaymentRepository repo, AppDbContext db) : IPaymentService
{
    public async Task<Result<PaymentResponseDto>> CreateAsync(CreatePaymentDto dto)
    {
        // 1. Validações de formato
        if (!Regex.IsMatch(dto.ReferenceMonth, @"^\d{4}-\d{2}$"))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.InvalidFormat, "Formato deve ser YYYY-MM");
        
        // 2. Validações de negócio
        var patient = await db.Patients.FindAsync(dto.PatientId);
        if (!patient.IsActive)
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.InactivePatient, "Paciente inativo");
        
        if (await repo.ExistsAsync(dto.PatientId, dto.ReferenceMonth))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.DuplicatePayment, "Já existe pagamento para este mês");
        
        // 3. Cria a entidade
        var payment = new Payment { ... };
        
        // 4. Persiste
        await repo.AddAsync(payment);
        
        // 5. Retorna sucesso
        return Result<PaymentResponseDto>.Ok(ToDto(payment));
    }
}
```

**⚠️ Regras importantes:**
1. Services NUNCA retornam entidades diretamente → sempre DTOs
2. Services NUNCA lançam exceções para erros de negócio → usam Result<T>
3. Services podem usar múltiplos Repositories

---

### 7. 📂 `Data/` - Contexto do Entity Framework

**Responsabilidade:** Configurar a conexão com o banco de dados.

**📋 Conteúdo:**
```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    // Cada DbSet = uma tabela no banco
    public DbSet<User> Users { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<MedicalRecord> MedicalRecords { get; set; }
    public DbSet<Plans> Plans { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<WhatsAppLog> WhatsAppLogs { get; set; }
}
```

---

### 8. 📂 `Migrations/` - Controle de Versão do Banco

**Responsabilidade:** Manter o histórico de alterações do esquema do banco.

**🎯 Para que serve:**
- Criar tabelas
- Alterar colunas
- Adicionar índices
- Versionar mudanças

**⚙️ Comandos úteis:**
```bash
dotnet ef migrations add NomeDaMigration  # Cria nova migration
dotnet ef database update                  # Aplica migrations no banco
```

---

### 9. 📂 `Services/` (Background Jobs)

**Arquivos especiais que herdam de `BackgroundService`:**

| Job | O que faz | Frequência |
|-----|-----------|------------|
| `AppointmentStatusUpdater` | Cancela consultas agendadas que já passaram | A cada 1 hora |
| `AppointmentReminderJob` | Envia lembretes de consulta via WhatsApp (23-25h antes) | A cada 1 hora |
| `PaymentReminderJob` | Envia lembretes de pagamento via WhatsApp (23-25h antes) | A cada 1 hora |

---

## 🔄 Fluxo Completo de uma Requisição

Vamos acompanhar o fluxo de **criar um pagamento**:

```
1️⃣ CLIENTE
   ↓ POST /api/payments
   ↓ { "patientId": 1, "planId": 2, "referenceMonth": "2026-03" }

2️⃣ CONTROLLER (PaymentsController.cs)
   ↓ Recebe o JSON → converte em CreatePaymentDto
   ↓ Chama: await _service.CreateAsync(dto)

3️⃣ SERVICE (PaymentService.cs)
   ↓ Valida formato do mês (YYYY-MM)
   ↓ Busca paciente no banco (via DbContext)
   ↓ Verifica se paciente está ativo
   ↓ Verifica se já existe pagamento para o mês (via Repository)
   ↓ Cria objeto Payment
   ↓ Chama: await _repository.AddAsync(payment)

4️⃣ REPOSITORY (PaymentRepository.cs)
   ↓ Adiciona ao DbContext: db.Payments.Add(payment)
   ↓ Salva: await db.SaveChangesAsync()

5️⃣ BANCO DE DADOS (SQLite)
   ↓ INSERT INTO Payments (...)

6️⃣ RETORNO (caminho inverso)
   ↓ Repository retorna Payment
   ↓ Service converte para PaymentResponseDto
   ↓ Service retorna Result<PaymentResponseDto>.Ok(dto)
   ↓ Controller retorna HTTP 201 Created
```

---

## 🧪 Padrões e Boas Práticas

### 1. Result Pattern

```csharp
// ❌ Antes (sem padrão)
public Payment CreatePayment(CreatePaymentDto dto)
{
    if (!patient.IsActive)
        throw new Exception("Paciente inativo");  // 😱 Exceção custosa!
    return payment;
}

// ✅ Depois (com Result Pattern)
public Result<Payment> CreatePayment(CreatePaymentDto dto)
{
    if (!patient.IsActive)
        return Result<Payment>.Fail(ErrorCodes.InactivePatient, "Paciente inativo");
    return Result<Payment>.Ok(payment);
}
```

### 2. Injeção de Dependência

```csharp
// ❌ Antes (acoplamento forte)
public class PaymentService
{
    private readonly PaymentRepository _repo = new();  // 😱 Não testável!
}

// ✅ Depois (inversão de controle)
public class PaymentService(IPaymentRepository repo)  // 😊 Interface!
{
    private readonly IPaymentRepository _repo = repo;
}
```

### 3. Separação de Responsabilidades

| Responsabilidade | Onde fica |
|------------------|-----------|
| Receber HTTP | Controller |
| Lógica de negócio | Service |
| Acesso a dados | Repository |
| Estrutura dos dados | Model |
| Formato da API | DTO |
| Resultados de operações | Result<T> |

---

## 🎓 Checklist para Desenvolver um Novo Endpoint

Quando for criar uma nova funcionalidade, siga este checklist:

### 1. DTOs
- [ ] `Create{Nome}Dto.cs` - Dados de entrada para criação
- [ ] `Update{Nome}Dto.cs` - Dados de entrada para atualização  
- [ ] `{Nome}ResponseDto.cs` - Dados de saída da API

### 2. Repository
- [ ] `I{Nome}Repository.cs` - Interface na pasta `Interfaces/`
- [ ] `{Nome}Repository.cs` - Implementação
- [ ] Métodos: GetById, GetPaged, Add, SaveChanges, Delete

### 3. Service
- [ ] `I{Nome}Service.cs` - Interface na pasta `Interfaces/`
- [ ] `{Nome}Service.cs` - Implementação
- [ ] Validações de negócio
- [ ] Retornar `Result<T>`

### 4. Controller
- [ ] Injetar `I{Nome}Service` no construtor
- [ ] Métodos: GetAll, GetById, Create, Update, Delete
- [ ] Converter Result em HTTP Status Code

### 5. Registro no DI
- [ ] Adicionar no `Program.cs`:
```csharp
builder.Services.AddScoped<I{Nome}Repository, {Nome}Repository>();
builder.Services.AddScoped<I{Nome}Service, {Nome}Service>();
```

---

## 🆘 Resolução de Problemas Comuns

### "Não consigo acessar o banco nos testes"
Verifique se o `CustomWebApplicationFactory` está configurando o banco em memória ou arquivo temporário.

### "Circular reference ao serializar JSON"
Sempre retorne DTOs, nunca entidades diretamente. As entidades têm navigation properties que criam loops.

### "Mudança no banco não reflete no código"
Execute:
```bash
dotnet ef migrations add DescricaoDaMudanca
dotnet ef database update
```

### "Service retorna erro mas Controller retorna 500"
Verifique se o Controller está tratando o `Result.IsSuccess` corretamente e retornando o status code adequado.

---

## 📚 Recursos Adicionais

- **Entity Framework Core:** https://docs.microsoft.com/ef/core/
- **ASP.NET Core Web API:** https://docs.microsoft.com/aspnet/core/web-api/
- **Repository Pattern:** https://docs.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design

---

**🎉 Parabéns! Agora você entende a arquitetura do projeto!**

Se tiver dúvidas, procure por exemplos existentes no código - o módulo **Payments** é o mais completo e serve de referência.
