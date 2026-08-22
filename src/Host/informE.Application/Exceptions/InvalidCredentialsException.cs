namespace informE.Application.Exceptions;

// Mensagem deliberadamente genérica: não revela se o e-mail existe ou se a senha
// está errada. Diferenciar os dois permitiria enumerar contas válidas.
public class InvalidCredentialsException() : Exception("E-mail ou senha inválidos.");
