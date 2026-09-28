namespace informE.Application.Exceptions;

// Mensagem genérica pelo mesmo motivo do InvalidCredentialsException: sessão
// inexistente, expirada, revogada e segredo errado caem todas aqui. Diferenciar
// diria a um atacante se um sessionId roubado ainda existe.
public class InvalidRefreshTokenException() : Exception("Refresh token inválido ou expirado.");
