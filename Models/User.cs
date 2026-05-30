// Define o namespace — organiza o arquivo dentro do projeto.
// Convenção: NomeProjeto.Pasta
namespace ProjetoLP.API.Models;

// Enum define um conjunto fixo de valores possíveis.
// Evita usar strings soltas como "admin" ou "patient" no código.
public enum UserRole
{
    Admin,   // Perfil com acesso total ao sistema
    Fisio  // Perfil com acesso restrito (próprios dados)
}

// Classe que representa a tabela "Users" no banco de dados.
// O EF Core usa essa classe para gerar a tabela automaticamente.
public class User
{
    // Chave primária — o EF Core reconhece "Id" automaticamente e gera autoincrement.
    public int Id { get; set; }

    // Campos de Dados Pessoais 
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string CPF { get; set; } = string.Empty;
    public string Rg { get; set; } = string.Empty;

// Campos de endereço — podem ser usados para contato ou faturamento.
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;

    // Nunca salvamos a senha diretamente — apenas o hash criptografado.
    public string PasswordHash { get; set; } = string.Empty;

    // Enum como tipo — o EF Core salva como número inteiro no banco (0 = Admin, 1 = Fisio).
    // Valor padrão: todo usuário criado começa como Fisio.
    public UserRole Role { get; set; } = UserRole.Fisio;

    // DateTime.UtcNow preenche automaticamente com a data/hora atual no momento da criação.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties — não viram colunas no banco.
    // Permitem acessar os dados relacionados: user.Appointments, user.Payments, etc.
    // ICollection representa uma coleção (lista) de registros filhos.
    // "[]" inicializa a lista vazia para evitar NullReferenceException.
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
