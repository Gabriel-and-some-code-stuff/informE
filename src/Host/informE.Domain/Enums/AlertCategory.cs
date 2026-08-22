namespace informE.Domain.Enums;

// As 6 faixas do gráfico "Histórico de Alertas" (stacked bar). Vem do documento
// de análise do Figma — "PADRÕES DE ALERTA": Hardware (CPU+RAM), Agente (roxo,
// o sistema manda automaticamente), Rede (azul, rede/wi-fi), Windows (verde),
// Offline (cinza), Armazenamento (amarelo).
//
// É agrupamento de APRESENTAÇÃO: 11 AlertType técnicos colapsam em 6 faixas.
// A cor é do frontend, não do domínio.
public enum AlertCategory { Hardware, Armazenamento, Rede, Windows, Agente, Offline }
