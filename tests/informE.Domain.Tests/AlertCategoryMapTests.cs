using informE.Domain;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

public class AlertCategoryMapTests
{
    // Mesma armadilha do MachineActionCatalog: mapa indexado por enum. Sem este
    // teste, adicionar um AlertType e esquecer a faixa só estoura quando aquele
    // alerta aparece no gráfico.
    [Fact]
    public void Todo_AlertType_deve_ter_faixa_no_grafico()
    {
        foreach (var tipo in Enum.GetValues<AlertType>())
            Assert.True(Enum.IsDefined(AlertCategoryMap.Of(tipo)));
    }

    // As 6 faixas do documento de análise do Figma têm que estar todas em uso —
    // faixa sem nenhum tipo apontando pra ela é legenda morta na tela.
    [Fact]
    public void Todas_as_6_faixas_devem_ter_ao_menos_um_tipo()
    {
        var emUso = Enum.GetValues<AlertType>().Select(AlertCategoryMap.Of).Distinct();

        Assert.Equal(Enum.GetValues<AlertCategory>().Length, emUso.Count());
    }

    [Theory]
    [InlineData(AlertType.HighCpu, AlertCategory.Hardware)]
    [InlineData(AlertType.HighRam, AlertCategory.Hardware)]
    [InlineData(AlertType.DiskFull, AlertCategory.Armazenamento)]
    [InlineData(AlertType.HighPing, AlertCategory.Rede)]
    [InlineData(AlertType.PendingUpdates, AlertCategory.Windows)]
    [InlineData(AlertType.ServiceStopped, AlertCategory.Agente)]
    [InlineData(AlertType.DeviceOffline, AlertCategory.Offline)]
    public void Deve_agrupar_conforme_o_documento(AlertType tipo, AlertCategory esperada)
    {
        Assert.Equal(esperada, AlertCategoryMap.Of(tipo));
    }
}
