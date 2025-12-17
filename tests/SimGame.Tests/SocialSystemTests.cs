using System.Collections.Generic;
using SimGame.Core;
using Xunit;
using SimGame.Tests;

public class SocialSystemTests
{
    [Fact]
    public void Pawn_Gains_Social_When_Near_Other_Pawn()
    {
        // Arrange: Define Social need and two pawns
        var sim = new SimGame.Tests.TestSimulationBuilder()
            .DefineNeed("Social", "Social", decayPerTick: 0f)
            .AddPawn("A", 5, 5, new Dictionary<string, float> { { "Social", 50f } })
            .AddPawn("B", 6, 5, new Dictionary<string, float> { { "Social", 50f } })
            .Build();

        var content = sim.Content;
        var pawn1 = sim.GetPawnByName("A");
        var pawn2 = sim.GetPawnByName("B");
        var socialNeedId = content.GetNeedId("Social");
        Assert.True(socialNeedId.HasValue, "Social need ID should not be null");

        // Act: Run several ticks
        sim.RunTicks(10);

        // Assert: Both pawns should have gained social need
        Assert.True(sim.Entities.Needs[pawn1.Value].Needs[socialNeedId.Value] > 50f);
        Assert.True(sim.Entities.Needs[pawn2.Value].Needs[socialNeedId.Value] > 50f);
    }

    [Fact]
    public void Pawn_Does_Not_Gain_Social_When_Alone()
    {
        // Arrange: Define Social need and one pawn
        var sim = new SimGame.Tests.TestSimulationBuilder()
            .DefineNeed("Social", "Social", decayPerTick: 0f)
            .AddPawn("Solo", 5, 5, new Dictionary<string, float> { { "Social", 50f } })
            .Build();

        var content = sim.Content;
        var pawn1 = sim.GetPawnByName("Solo");
        var socialNeedId = content.GetNeedId("Social");
        Assert.True(socialNeedId.HasValue, "Social need ID should not be null");

        // Act: Run several ticks
        sim.RunTicks(10);

        // Assert: Pawn should not have gained social need
        Assert.Equal(50f, sim.Entities.Needs[pawn1.Value].Needs[socialNeedId.Value]);
    }
}
