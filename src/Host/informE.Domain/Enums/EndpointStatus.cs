namespace informE.Domain.Enums;

// RF04: Online (heartbeat saudável), Offline (sem heartbeat no prazo), Degraded
// (heartbeat recebido mas com uso de recurso acima do limiar), Unknown (ainda não
// contatou o Host desde o enroll — estado inicial, distinto de Offline).
public enum EndpointStatus { Online, Offline, Degraded, Unknown }
