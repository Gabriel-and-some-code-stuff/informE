namespace informE.Application.Exceptions;

// Token de enrollment inexistente, já usado ou expirado (RF01/RF12).
public class EnrollmentTokenInvalidException() : Exception("Token de registro inválido, já utilizado ou expirado.");
