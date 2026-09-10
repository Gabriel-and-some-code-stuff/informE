namespace informE.Application;

// A regra de senha mora AQUI, num lugar so.
//
// POR QUE EXISTE: o minimo de 8 caracteres estava dentro do CreateUserUseCase e
// mais nada o conhecia. Resultado: criar usuario exigia 8 caracteres, mas
// REDEFINIR senha aceitava "123" -- e redefinir e justamente o caminho por onde
// uma conta de administrador troca de senha. A porta dos fundos era mais fraca
// que a da frente.
//
// Nao esta no Domain porque a Application e quem conhece o fluxo de senha; o
// Domain guarda apenas o HASH e nunca ve a senha em claro (ver User.PasswordHash
// e IPasswordHasher).
public static class PoliticaDeSenha
{
    public const int TamanhoMinimo = 8;

    public static void Validar(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < TamanhoMinimo)
            throw new ArgumentException(
                $"A senha precisa ter ao menos {TamanhoMinimo} caracteres.");
    }
}
