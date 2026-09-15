namespace informE.Application;

// Só e-mail institucional entra no informE.
//
// POR QUE AQUI E NÃO NO DOMAIN: a lista de domínios vem de configuração e muda
// por instalação (uma Etec aceita cps.sp.gov.br; outra instância, o domínio da
// empresa). O Domain não lê configuração — `User.ValidateEmail` continua
// cuidando só do FORMATO do endereço.
//
// Lista vazia = sem restrição. É o que mantém teste e script antigo funcionando
// sem precisar conhecer esta classe.
public class DominioDeEmailPolicy(IReadOnlyList<string> dominiosPermitidos)
{
    public void Validar(string email)
    {
        if (dominiosPermitidos.Count == 0)
            return;

        // O formato já foi validado pelo Domain; aqui só interessa o que vem
        // depois do último '@'. LastIndexOf e não IndexOf: "a@b"@dominio é
        // endereço legal e o domínio é o do fim.
        var arroba = email.LastIndexOf('@');

        var dominio = arroba >= 0 && arroba < email.Length - 1
            ? email[(arroba + 1)..]
            : string.Empty;

        if (dominiosPermitidos.Any(d => string.Equals(d, dominio, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ArgumentException(
            $"E-mail '{email}' não é institucional. Domínios aceitos: {string.Join(", ", dominiosPermitidos)}.");
    }
}
