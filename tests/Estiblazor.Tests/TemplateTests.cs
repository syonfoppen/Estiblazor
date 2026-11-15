using Estiblazor.Domain;
using Xunit;

namespace Estiblazor.Tests;

public sealed class TemplateTests
{
    [Fact]
    public void EveryBuiltInTemplateHasAUniqueNameAndMeetsRoomValidation()
    {
        Assert.Equal(RoomTemplates.All.Length, RoomTemplates.All.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var template in RoomTemplates.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Description));
            var configuration = Configuration.Template(template.Name).Validate();
            Assert.Equal(template.Phases.Length, configuration.Phases.Length);
        }
        Assert.Throws<RoomException>(() => Configuration.Template("Unknown template"));
    }

    [Fact]
    public void CustomizingOneRoomDoesNotChangeTheCatalogOrAnotherRoom()
    {
        var first = Configuration.Template("T-shirt sizes");
        var second = Configuration.Template("T-shirt sizes");
        var custom = first with
        {
            Name = "My team",
            Phases = first.Phases.SetItem(0, first.Phases[0] with { Options = first.Phases[0].Options.SetItem(0, "Tiny") })
        };
        Assert.Equal("Tiny", custom.Phases[0].Options[0]);
        Assert.Equal("XXS", second.Phases[0].Options[0]);
        Assert.Equal("XXS", Configuration.Template("T-shirt sizes").Phases[0].Options[0]);
    }
}
