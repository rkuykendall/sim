using SimGame.Tests;
using Xunit;

public class SocialSystemTests
{
    [Fact]
    public void Pawn_Gains_Social_When_Near_Other_Pawn()
    {
        // Arrange: Define Social need and two pawns
        var builder = new TestSimulationBuilder();
        var socialNeedId = builder.DefineNeed(key: "Social", decayPerTick: 0f);
        builder.AddPawn("A", 5, 5, new Dictionary<int, float> { { socialNeedId, 50f } });
        builder.AddPawn("B", 6, 5, new Dictionary<int, float> { { socialNeedId, 50f } });
        var sim = builder.Build();
        var pawn1 = sim.GetPawnByName("A");
        var pawn2 = sim.GetPawnByName("B");
        // socialNeedId is always valid (int)

        // Act: Run several ticks
        sim.RunTicks(10);

        // Assert: Both pawns should have gained social need
        Assert.True(sim.Entities.Needs[pawn1.Value].Needs[socialNeedId] > 50f);
        Assert.True(sim.Entities.Needs[pawn2.Value].Needs[socialNeedId] > 50f);
    }

    [Fact]
    public void Pawn_Does_Not_Gain_Social_When_Alone()
    {
        // Arrange: Define Social need and one pawn
        var builder = new TestSimulationBuilder();
        var socialNeedId = builder.DefineNeed(key: "Social", decayPerTick: 0f);
        builder.AddPawn("Solo", 5, 5, new Dictionary<int, float> { { socialNeedId, 50f } });
        var sim = builder.Build();

        var pawn1 = sim.GetPawnByName("Solo");
        // Act: Run several ticks
        sim.RunTicks(10);
        // Assert: Pawn should not have gained social need
        Assert.Equal(50f, sim.Entities.Needs[pawn1.Value].Needs[socialNeedId]);
    }
}
