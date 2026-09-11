namespace informE.Agent.Worker;

// Um lugar só decide se o agente aceita certificado não confiável — usado pelo
// HttpClient do enroll (Program.cs) e pelo handshake do hub (AgentWorker).
//
// Duplicar essa decisão nos dois pontos era o caminho para ligar num e esquecer
// no outro, e o sintoma seria um agente que registra e nunca conecta.
public static class CertificadoDeDesenvolvimento
{
    public static HttpMessageHandler CriarHandler(AgentOptions opcoes)
    {
        var handler = new HttpClientHandler();

        if (opcoes.AceitarCertificadoNaoConfiavel)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return handler;
    }
}
