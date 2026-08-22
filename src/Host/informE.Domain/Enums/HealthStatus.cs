namespace informE.Domain.Enums;

// Saúde do recurso — coluna "Saúde" da tela de Equipamentos, dimensão
// independente de EndpointStatus. Um device pode estar Online + Critico
// (PC-03 na tela: conectado, mas disco em 230/256 GB).
//
// Erro é o par natural de Offline: sem telemetria não há como avaliar saúde,
// e a tela mostra "—" em RAM/Disco/Uptime nessas linhas.
public enum HealthStatus { Saudavel, Aviso, Critico, Erro }
