using informE.Domain;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

public class MachineActionCatalogTests
{
    // O catálogo é um dicionário indexado pelo enum. Sem este teste, adicionar
    // um valor em MachineActionKind e esquecer a entrada aqui só estoura em
    // runtime, quando alguém escolher aquela ação no dropdown.
    [Fact]
    public void Todo_MachineActionKind_DeveTerDefinicaoNoCatalogo()
    {
        foreach (var kind in Enum.GetValues<MachineActionKind>())
        {
            var definition = MachineActionCatalog.Get(kind);

            Assert.Equal(kind, definition.Kind);
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Script));
        }
    }

    [Fact]
    public void All_DeveExporTodasAsAcoesParaODropdown()
    {
        Assert.Equal(Enum.GetValues<MachineActionKind>().Length, MachineActionCatalog.All.Count);
    }
}
