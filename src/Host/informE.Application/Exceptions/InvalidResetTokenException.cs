namespace informE.Application.Exceptions;

// Token de redefinição inexistente, expirado, já usado, ou com segredo que não
// bate. Mensagem única de propósito: não conta ao atacante QUAL das quatro
// coisas falhou.
public class InvalidResetTokenException()
    : Exception("Link de redefinição inválido ou expirado. Solicite um novo.");
