namespace informE.Application.Exceptions;

// docs/politica-login-sessao.md §2.1: Admin/SuperAdmin bloqueiam o 4º dispositivo
// em vez de revogar o mais antigo. O usuário precisa revogar uma sessão existente.
public class DeviceLimitReachedException(int limite)
    : Exception($"Limite de {limite} dispositivos ativos atingido. Revogue uma sessão antes de entrar em outro dispositivo.");
