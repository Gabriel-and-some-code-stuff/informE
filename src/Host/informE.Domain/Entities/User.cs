using informE.Domain.Enums;
using System.Text.RegularExpressions;

namespace informE.Domain.Entities;

public class User
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Guid Id { get; set; }

    // "ID da conta: USR-0001" na tela de Meu Perfil. Mesma ideia do MachineTask.Code:
    // Guid é a chave, isto é o rótulo humano.
    // `null!` e nao `string.Empty`: o valor vem do banco (sequence). O EF so
    // OMITE a coluna do INSERT quando ve o sentinel de nao-preenchido, que
    // para string e `null`. Com string.Empty ele mandava '' em toda linha e o
    // indice unico rejeitava a segunda.
    public string Code { get; set; } = null!;

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Argon2id via IPasswordHasher
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Coluna "Status" (Ativo/Inativo) da tela de Administração de Contas.
    public bool IsActive { get; set; } = true;

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];

    public User() { }

    // Construtor padrão

    public User(string username, string email, string passwordHash, UserRole role)
    {
        // Os tres validadores LANCAM quando o valor e invalido — por isso a
        // atribuicao e direta. Antes, ValidateEmail e ValidateRole devolviam
        // `false` e o `if` simplesmente NAO atribuia: e-mail invalido virava
        // string.Empty em silencio. Como `users.email` e UNIQUE e NOT NULL, o
        // primeiro usuario ruim nascia sem e-mail (e sem conseguir logar) e o
        // segundo estourava 23505 -> 500.
        ValidateUsername(username);
        Username = username;

        ValidateEmail(email);
        Email = Normalizar(email);

        ValidateRole(role);
        Role = role;

        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // Criação dos métodos

    public void UpdateUsername(string username)
    {
        ValidateUsername(username);
        Username = username;
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("O nome de usuário não pode ser vazio.");

        if (username.Length > 60)
            throw new ArgumentException("O nome de usuário ultrapassou o limite de caracteres.");

        if (!username.All(char.IsLetterOrDigit))
            throw new ArgumentException("O nome de usuário contém caracteres inválidos.");
    }

    // Valida só o FORMATO. A restrição de domínio institucional (@cps.sp.gov.br)
    // é da Application, não daqui: a lista de domínios permitidos vem de
    // configuração e varia por instalação, e o Domain não lê configuração.
    // Ver DominioDeEmailPolicy e AuthOptions.
    // E-mail e guardado em minusculas e sem espaco nas pontas.
    //
    // O BUG QUE ISTO CONSERTA: a busca por e-mail era `u.Email == email`, exata
    // e sensivel a caixa. Entao "admin@cps.sp.gov.br" logava e
    // "Admin@cps.sp.gov.br" devolvia 401 -- a MESMA conta. Como teclado de
    // celular e campo de WebView capitalizam a primeira letra sozinhos, o login
    // funcionava ou nao dependendo de como a pessoa digitou. Chegou a ser
    // relatado como "login hiper inconsistente", e era exatamente isso.
    //
    // A parte de dominio de um e-mail e insensivel a caixa por norma (RFC 5321),
    // e ninguem espera que Admin@ seja outra conta que admin@. Normalizar na
    // ESCRITA (aqui) e na LEITURA (UserRepository) fecha os dois lados: sem o
    // lado da escrita, o indice unico de `users.email` aceitaria admin@ e Admin@
    // como contas distintas.
    private static string Normalizar(string email) =>
        email.Trim().ToLowerInvariant();

    private static void ValidateEmail(string email)
    {
        if (!EmailRegex.IsMatch(email))
            throw new ArgumentException($"E-mail inválido: '{email}'.");
    }

    private static void ValidateRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentException($"Papel {role} não existe.");
    }

    // Métodos de domínio
    public void UpdateEmail(string email)
    {
        ValidateEmail(email);
        Email = Normalizar(email);
    }

    // Só troca o flag. A revogação das sessões ativas NÃO acontece aqui de
    // propósito: depende das Sessions estarem carregadas, e um método de domínio
    // que falha silencioso quando a coleção não veio no Include é uma armadilha.
    // Quem desativa é responsável por revogar as sessões via IUserRepository —
    // ver docs/politica-login-sessao.md §3.5.
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    // Promoção/rebaixamento. Quem PODE fazer isso é decidido na Application
    // (ChangeUserRoleUseCase) — o Domain só garante que o papel existe.
    public void ChangeRole(UserRole role)
    {
        ValidateRole(role);
        Role = role;
    }

    // Recebe o hash já calculado — a responsabilidade de hashar é do IPasswordHasher na Application.
    public void ChangePassword(string newHash)
    {
        if (string.IsNullOrWhiteSpace(newHash))
            throw new ArgumentException("O hash da senha não pode ser vazio.");

        PasswordHash = newHash;
    }
}
