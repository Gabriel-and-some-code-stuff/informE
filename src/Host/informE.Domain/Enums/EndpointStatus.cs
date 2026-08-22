namespace informE.Domain.Enums;

// Conectividade — coluna "Conexão" da tela de Equipamentos. Responde só
// "o agente está falando com o Host?". Saúde do recurso é outra dimensão,
// ver HealthStatus (a tela trata as duas como colunas separadas).
//
// Unknown = enrollado mas nunca reportou nada ainda; distinto de Offline,
// que é "já reportou antes e parou".
public enum EndpointStatus { Online, Offline, Unknown }
