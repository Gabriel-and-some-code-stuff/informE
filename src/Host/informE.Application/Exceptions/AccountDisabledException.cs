namespace informE.Application.Exceptions;

// Conta existe e a senha está certa, mas foi desativada (Status "Inativo" na tela
// de Administração de Contas). Separado de InvalidCredentialsException porque aqui
// a credencial JÁ foi validada — não há risco de enumeração.
public class AccountDisabledException() : Exception("Esta conta está desativada. Procure um administrador.");
