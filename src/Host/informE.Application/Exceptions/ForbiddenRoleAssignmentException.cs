using informE.Domain.Enums;

namespace informE.Application.Exceptions;

// Documento de análise do Figma: "o ADMIN pode criar ADMIN E VIEWER; o SUPERADMIN
// pode criar ADMIN, VIEWER e SUPERADMIN".
public class ForbiddenRoleAssignmentException(UserRole criador, UserRole alvo)
    : Exception($"Um {criador} não pode criar usuário com papel {alvo}.");
