namespace informE.Agent.Worker;

// Um lugar só monta o handler HTTP do agente — usado pelo HttpClient do enroll
// (Program.cs) e pelo handshake do hub (AgentWorker).
//
// Duplicar essa decisão nos dois pontos era o caminho para ligar num e esquecer
// no outro, e o sintoma seria um agente que registra e nunca conecta.
public static class CertificadoDeDesenvolvimento
{
    public static HttpMessageHandler CriarHandler(AgentOptions opcoes)
    {
        var handler = new HttpClientHandler
        {
            // O Server está SEMPRE na mesma rede do agente — é o que "on-premise"
            // significa. Nenhum tráfego daqui deve sair pelo proxy da instituição.
            //
            // Sem isto o agente herda o proxy do sistema (HttpClient.DefaultProxy:
            // variáveis HTTP_PROXY/HTTPS_PROXY ou, faltando elas, o registro do
            // WinINET). Numa máquina de laboratório com proxy configurado e sem
            // lista de exceção — o caso da Etec, onde ProxyOverride vem vazia —
            // tanto o POST do enroll quanto o handshake de /hubs/agent saem rumo
            // ao proxy, que não alcança o IP interno do Server e devolve
            // 503 Service Unavailable.
            //
            // Um Windows Service não roda dentro de um shell, então nem as
            // variáveis de ambiente que trariam a exceção existem para ele: o
            // agente é MAIS exposto a isso que o Desktop, não menos.
            UseProxy = false,
            Proxy = null
        };

        if (opcoes.AceitarCertificadoNaoConfiavel)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return handler;
    }
}
