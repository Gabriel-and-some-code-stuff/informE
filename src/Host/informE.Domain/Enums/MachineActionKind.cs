namespace informE.Domain.Enums;

// Catálogo fechado de ações — alimenta o dropdown "Ação" da tela de Nova
// Execução. O cliente escolhe da lista e NUNCA envia script: o script real
// mora no servidor (MachineActionCatalog), o que satisfaz RF14 (integridade
// da origem do comando) sem precisar assinar payload vindo da UI.
//
// Só ações sem parâmetro por ora. "Instalação de Software" e "Backup
// Automático" aparecem na tela mas exigem argumento (qual pacote? destino?),
// e argumento de ação é um modelo que ainda não existe — ver docs.
public enum MachineActionKind
{
    // A mais inofensiva do catálogo: só LÊ e reporta, não altera nada na máquina.
    // É a ação certa para demonstrar o ciclo completo (dispara -> executa -> volta
    // com output) sem risco nenhum. Roda em segundos.
    InformacoesDoSistema,

    LimpezaDeDisco,
    AtualizacaoWinGet,
    AtualizacaoWindows,
    Reinicializacao,
    Desligamento,
    DiagnosticoDeRede
}
