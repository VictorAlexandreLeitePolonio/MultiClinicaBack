# ProjetoLP — API Back-end

API REST desenvolvida em **.NET 10** para gerenciamento completo de uma clínica de fisioterapia. Este sistema foi construído como projeto pessoal para a clínica da minha mãe, com o objetivo de digitalizar e centralizar toda a operação clínica — de pacientes a controle financeiro.

---

## ✨ Arquitetura

Este projeto segue uma arquitetura em camadas moderna e desacoplada:

```
┌─────────────────────────────────────────────────────────────┐
│  🎮 CONTROLLERS (Thin)                                       │
│  Recebem HTTP → Chamam Services → Retornam resposta         │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  ⚙️ SERVICES (Camada de Negócio)                             │
│  Contêm TODA a lógica de negócio                           │
│  Retornam Result<T> (sucesso ou erro tipado)              │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  🗄️ REPOSITORIES (Acesso a Dados)                           │
│  Queries EF Core, sem lógica de negócio                    │
└─────────────────────────────────────────────────────────────┘
```

**Padrões implementados:**
- **Repository Pattern** — Isolamento do acesso a dados
- **Service Layer** — Centralização da lógica de negócio
- **Result Pattern** — Tratamento de erros sem exceções
- **Dependency Injection** — Inversão de controle em todas as camadas

📚 [Leia o guia completo de arquitetura →](ARQUITETURA.md)

---

## Para quem é

Sistema interno utilizado pela administração e pelos fisioterapeutas da clínica. Acesso controlado por perfis:

- **Admin** — acesso total: pacientes, agenda, pagamentos, planos, prontuários, usuários e financeiro
- **Fisio** — acesso restrito: pacientes, agenda e prontuários

---

## 🛠 Tecnologias

| Tecnologia | Uso |
|-----------|-----|
| .NET 10 | Framework principal |
| ASP.NET Core | Web API / Controllers |
| Entity Framework Core 10 | ORM / migrations |
| SQLite | Banco de dados |
| BCrypt.Net | Hash de senhas |
| JWT Bearer | Autenticação via cookies HttpOnly |
| Swagger | Documentação da API |
| xUnit | Testes de integração |

---

## 📁 Estrutura do Projeto

```
ProjetoLP.API/
├── Common/                 # Result<T> e ErrorCodes
├── Controllers/            # Thin controllers (apenas HTTP)
├── DTOs/                   # Data Transfer Objects
├── Data/                   # AppDbContext (EF Core)
├── Migrations/             # Migrations do banco
├── Models/                 # Entidades do banco
├── Repositories/           # Acesso a dados
│   └── Interfaces/         # Contratos dos repositories
├── Services/               # Lógica de negócio
│   └── Interfaces/         # Contratos dos services
└── wwwroot/uploads/        # Arquivos (contratos, exames)

ProjetoLP.Tests/            # Testes de integração (85 testes)
```

---

## 📦 Módulos

### Autenticação (`/api/auth`)
- Login com JWT armazenado em cookie HttpOnly
- Expiração de 8 horas
- Controle de acesso por roles (`Admin`, `Fisio`)

### Pacientes (`/api/patients`)
- CRUD completo com paginação
- Filtros por nome, status ativo/inativo, status de agendamento e pagamento
- Alternância de status ativo/inativo (PATCH)
- Proteção contra exclusão de pacientes com vínculos
- Perfil completo com histórico de consultas, pagamentos e prontuários

### Agenda (`/api/appointments`)
- Agendamento de consultas com validação de data futura
- Filtros por data, período e status
- Regras de negócio: não reagenda consultas concluídas ou canceladas
- Proteção contra agendamento de pacientes inativos

### Prontuários (`/api/medicalrecords`)
- Registro clínico completo por paciente
- Upload de contrato em PDF (limite 20MB)
- Upload de exames de imagem JPG/PNG (limite 10MB)
- Filtros por paciente, data e profissional

### Pagamentos (`/api/payments`)
- Controle de pagamentos mensais por paciente e plano
- Geração automática do valor a partir do plano vinculado
- Gerenciamento automático de `PaidAt` conforme status
- Filtros por paciente, status e mês de referência
- Proteção contra deleção de pagamentos já confirmados

### Planos (`/api/plans`)
- CRUD de planos com valor, tipo (Mensal/Avulso) e tipo de sessão
- Status ativo/inativo para controle de disponibilidade
- Proteção contra exclusão de planos com pagamentos associados

### Usuários (`/api/users`)
- Gerenciamento de usuários do sistema
- Proteção contra exclusão do último administrador
- Proteção contra exclusão de usuários com registros associados

### Financeiro (`/api/financial`)
- Registro de gastos mensais da clínica
- Balanço mensal: soma de gastos vs. soma de pagamentos recebidos
- Histórico de balanço dos últimos N meses (para gráfico de evolução)

### WhatsApp (integração interna)
- Envio automático de lembretes de consulta (24h antes)
- Envio automático de lembretes de pagamento (24h antes)
- Registro de logs para auditoria

---

## 🚀 Como rodar

### Pré-requisitos
- .NET 10 SDK
- SQLite (já incluído)

### Comandos

```bash
# Clone o repositório
git clone <url-do-repositorio>
cd ProjetoLP.API

# Restaurar dependências
dotnet restore

# Aplicar migrations e criar o banco
dotnet ef database update

# Rodar a API
dotnet run --launch-profile http
```

A API sobe em `http://localhost:5045` (HTTP) por padrão. Swagger disponível em `/swagger`.

### Seed de dados
Ao iniciar pela primeira vez, o sistema cria automaticamente:
- 2 usuários (Admin e Fisio)
- 5 planos de atendimento
- 5 pacientes de exemplo
- 5 agendamentos
- 8 prontuários
- 4 pagamentos

---

## 🧪 Testes

O projeto conta com **85 testes de integração** que cobrem todos os módulos da API:

```bash
# Rodar todos os testes
dotnet test

# Rodar com detalhes
dotnet test --verbosity normal

# Rodar testes de um módulo específico
dotnet test --filter "FullyQualifiedName~PaymentsTests"
```

### Cobertura de testes
- ✅ Autenticação (login, autorização)
- ✅ Pacientes (CRUD, filtros, toggle status)
- ✅ Agendamentos (CRUD, validações de data)
- ✅ Prontuários (CRUD, upload de arquivos)
- ✅ Pagamentos (CRUD, regras de negócio)
- ✅ Planos (CRUD, toggle status)
- ✅ Usuários (CRUD, proteção de admin)
- ✅ Financeiro (gastos, balanço mensal)

---

## 🔐 Segurança

- **Autenticação:** JWT em cookie HttpOnly (proteção contra XSS)
- **Senhas:** Hash BCrypt (nunca armazenadas em texto)
- **Autorização:** Controle por roles (`[Authorize(Roles = "Admin")]`)
- **Validação:** Todas as entradas validadas nos Services
- **Upload:** Validação de tipo MIME e tamanho de arquivo
- **CORS:** Configurado apenas para origem do frontend

---

## 📊 Padrões de Resposta

### Sucesso

```json
// Listas paginadas (GET /api/payments)
{
  "data": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": 25,
  "totalPages": 3
}

// Item único (GET /api/payments/1)
{
  "id": 1,
  "patientName": "Maria Oliveira",
  "amount": 350.00,
  "status": "Paid",
  ...
}
```

### Erro

```json
// Erro padronizado
{ "message": "Pagamento não encontrado." }

// Erros de validação retornam códigos HTTP apropriados:
// 400 - Bad Request (dados inválidos)
// 401 - Unauthorized (não autenticado)
// 403 - Forbidden (sem permissão)
// 404 - Not Found (recurso não existe)
// 409 - Conflict (conflito de dados)
```

---

## 📚 Documentação Adicional

| Arquivo | Descrição |
|---------|-----------|
| [`ARQUITETURA.md`](ARQUITETURA.md) | Guia completo da arquitetura em camadas |
| [`CLAUDE.md`](CLAUDE.md) | Contexto técnico para agentes de IA |
| [`AGENTS.md`](AGENTS.md) | Convenções e padrões do projeto |

---

## 🎯 O que aprendi construindo este projeto

Este foi meu primeiro projeto back-end completo em .NET, evoluindo de uma API simples para uma arquitetura production-ready. As principais habilidades desenvolvidas:

### Fundamentos
- **Arquitetura REST** — organização de rotas, verbos HTTP corretos, status codes semânticos
- **Entity Framework Core** — modelagem de entidades, relacionamentos, migrations, LINQ
- **Autenticação JWT** — tokens, claims, cookies HttpOnly, controle de acesso por roles

### Arquitetura e Padrões
- **Repository Pattern** — isolamento do acesso a dados, facilitando testes e manutenção
- **Service Layer** — centralização da lógica de negócio, separação de responsabilidades
- **Result Pattern** — tratamento de erros sem exceções, código mais previsível
- **Dependency Injection** — desacoplamento total entre camadas
- **Thin Controllers** — controllers que apenas recebem e respondem HTTP

### Boas Práticas
- **DTOs** — separação entre Model (banco) e DTO (API), evitando over-posting
- **Validação em camadas** — validações de entrada (Controller) e de negócio (Service)
- **Testes de integração** — 85 testes garantindo que tudo funciona
- **Background Services** — jobs automáticos para lembretes e manutenção
- **Integração com WhatsApp** — Evolution API para notificações automáticas

### Segurança
- Hash de senhas com BCrypt
- Nunca expor dados sensíveis nas respostas
- Mensagens de erro que não revelam informações internas
- Proteções contra exclusão de registros com dependências
- Upload de arquivos com validação de tipo e tamanho

---

## 📝 Licença

Este projeto é privado e foi desenvolvido para uso exclusivo da clínica.

---

**Desenvolvido com ❤️ para a clínica da minha mãe**
