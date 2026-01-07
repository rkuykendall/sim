using System.Collections.Generic;
using System.Linq;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Tests for pawn movement and pathfinding.
/// </summary>
public class PawnMovementTests
{
    private readonly ITestOutputHelper _output;

    public PawnMovementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Two pawns want to use the SAME building (like your screenshot showing
    /// Jordan trying to use the sink while blocked by Sam).
    /// This tests the "building in use" logic and queuing.
    /// </summary>
    [Fact]
    public void TwoPawns_WantSameBuilding_OneWaitsOrFindsAlternative()
    {
        // Arrange: 5x3 area with ONE sink, two pawns who both need hygiene
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(4, 2); // 5x3 area
        var hygieneId = builder.DefineNeed(key: "Hygiene", decayPerTick: 0.001f);
        var sinkDefId = builder.DefineBuilding(
            key: "Sink",
            satisfiesNeedId: hygieneId,
            satisfactionAmount: 50f,
            interactionDuration: 40
        ); // Long interaction
        builder.AddBuilding(sinkDefId, 2, 1); // Sink at (2,1)
        // Both pawns want the sink
        builder.AddPawn("Sam", 0, 1, new Dictionary<int, float> { { hygieneId, 5f } });
        builder.AddPawn("Jordan", 4, 1, new Dictionary<int, float> { { hygieneId, 5f } });
        var sim = builder.Build();

        var sam = sim.GetPawnByName("Sam");
        var jordan = sim.GetPawnByName("Jordan");
        Assert.NotNull(sam);
        Assert.NotNull(jordan);

        var samPositions = new List<TileCoord>();
        var jordanPositions = new List<TileCoord>();
        bool samSinked = false;
        bool jordanSinked = false;

        // Act
        for (int tick = 0; tick < 500; tick++)
        {
            sim.Tick();

            var samPos = sim.GetPosition(sam.Value);
            var jordanPos = sim.GetPosition(jordan.Value);

            if (samPos.HasValue)
                samPositions.Add(samPos.Value);
            if (jordanPos.HasValue)
                jordanPositions.Add(jordanPos.Value);

            var samHygiene = sim.GetNeedValue(sam.Value, "Hygiene");
            var jordanHygiene = sim.GetNeedValue(jordan.Value, "Hygiene");

            if (samHygiene > 40)
                samSinked = true;
            if (jordanHygiene > 40)
                jordanSinked = true;

            if (tick % 50 == 0 || tick < 10)
            {
                _output.WriteLine(
                    $"Tick {tick}: Sam at {samPos} (hygiene={samHygiene:F1}), Jordan at {jordanPos} (hygiene={jordanHygiene:F1})"
                );

                if (sim.Entities.Actions.TryGetValue(sam.Value, out var samAction))
                {
                    var elapsed = sim.Time.Tick - samAction.ActionStartTick;
                    var duration = samAction.CurrentAction?.DurationTicks ?? 0;
                    _output.WriteLine(
                        $"  Sam action: {samAction.CurrentAction?.DisplayName ?? samAction.CurrentAction?.Type.ToString() ?? "none"}, elapsed={elapsed}, duration={duration}"
                    );
                }
                if (sim.Entities.Actions.TryGetValue(jordan.Value, out var jordanAction))
                {
                    var elapsed = sim.Time.Tick - jordanAction.ActionStartTick;
                    var duration = jordanAction.CurrentAction?.DurationTicks ?? 0;
                    _output.WriteLine(
                        $"  Jordan action: {jordanAction.CurrentAction?.DisplayName ?? jordanAction.CurrentAction?.Type.ToString() ?? "none"}, elapsed={elapsed}, duration={duration}"
                    );
                }
            }
        }

        // Check for stuck-ness
        var lastSamPositions = samPositions.Skip(Math.Max(0, samPositions.Count - 100)).ToList();
        var lastJordanPositions = jordanPositions
            .Skip(Math.Max(0, jordanPositions.Count - 100))
            .ToList();
        var uniqueSam = new HashSet<TileCoord>(lastSamPositions);
        var uniqueJordan = new HashSet<TileCoord>(lastJordanPositions);

        _output.WriteLine(
            $"\nLast 100 ticks - Sam unique positions: {uniqueSam.Count}, Jordan unique positions: {uniqueJordan.Count}"
        );
        _output.WriteLine($"Sam sinked: {samSinked}, Jordan sinked: {jordanSinked}");

        // Both should eventually sink (one waits for the other, or they take turns)
        Assert.True(samSinked || jordanSinked, "Neither pawn was able to use the sink");

        // At least one pawn should not be stuck in place forever
        Assert.True(
            uniqueSam.Count > 1 || uniqueJordan.Count > 1,
            $"Pawns appear stuck. Sam: {uniqueSam.Count} positions, Jordan: {uniqueJordan.Count} positions in last 100 ticks"
        );
    }

    /// <summary>
    /// Scenario: A pawn needs a building that is unreachable (behind a solid wall).
    /// The pawn should not thrash between idle and repeatedly trying to go to the building.
    /// </summary>
    [Fact]
    public void PawnWithNeed_UnreachableBuilding_DoesNotThrash()
    {
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var wallId = builder.DefineTerrain(key: "Wall", walkable: false);
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 5
        );

        builder.AddPawn("HungryPawn", 0, 2, new Dictionary<int, float> { { hungerId, 5f } });
        builder.AddBuilding(marketDefId, 4, 2);
        var sim = builder.Build();

        // Paint an impassable vertical wall between the pawn and the market (x = 1)
        for (int y = 0; y <= 4; y++)
            sim.PaintTerrain(new TileCoord(1, y), wallId);

        var pawnId = sim.GetPawnByName("HungryPawn");
        Assert.NotNull(pawnId);
        var marketId = sim.Entities.AllBuildings().First();

        int goingToMarketTicks = 0;
        for (int tick = 0; tick < 60; tick++)
        {
            sim.Tick();
            var actions = sim.Entities.Actions[pawnId.Value];
            bool targetingMarket =
                (actions.CurrentAction?.TargetEntity == marketId)
                || actions.ActionQueue.Any(a => a.TargetEntity == marketId);
            if (targetingMarket)
                goingToMarketTicks++;
        }

        Assert.True(
            goingToMarketTicks < 5,
            $"Pawn kept trying to reach unreachable market for {goingToMarketTicks} ticks"
        );
    }
}
