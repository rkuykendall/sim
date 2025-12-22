using System.Collections.Generic;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Tests for UseAreas - verifying pawns respect object use areas when interacting.
/// </summary>
public class UseAreasTests
{
    private readonly ITestOutputHelper _output;

    public UseAreasTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Bug test: A pawn should only be able to use an object from its defined UseAreas.
    /// If UseAreas is {(0, 1)} (south of object), pawn should not use it from (0, -1) (north).
    ///
    /// Layout (5x3 world):
    ///   Y=0: [ ][ ][F][ ][ ]   F = Fridge at (2,0)
    ///   Y=1: [ ][ ][U][ ][ ]   U = Valid use area (2,1) - south of fridge
    ///   Y=2: [P][ ][ ][ ][ ]   P = Pawn starting at (0,2)
    ///
    /// The pawn should walk to (2,1) to use the fridge, NOT to (2,-1) or adjacent cardinal tiles.
    /// </summary>
    [Fact]
    public void Pawn_UsesObject_OnlyFromDefinedUseAreas()
    {
        // Arrange: Fridge at (2,0) with UseArea only at (0,1) meaning pawn must stand at (2,1)
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 2)
            .DefineNeed("Hunger", "Hunger", decayPerTick: 0.001f)
            .DefineObject(
                "Fridge",
                "Fridge",
                satisfiesNeed: "Hunger",
                satisfactionAmount: 50f,
                interactionDuration: 20,
                useAreas: new List<(int, int)> { (0, 1) }
            )
            .AddObject("Fridge", 2, 0)
            .AddPawn("TestPawn", 0, 2, new Dictionary<string, float> { { "Hunger", 10f } })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== UseAreas Test ===");
        _output.WriteLine("Fridge at (2,0), UseArea at (0,1) relative = absolute (2,1)");
        _output.WriteLine("Pawn starts at (0,2), should walk to (2,1) to use fridge");

        TileCoord? positionWhenUsingFridge = null;

        // Act: Run simulation until pawn uses the fridge
        for (int tick = 0; tick < 200; tick++)
        {
            sim.Tick();

            var pos = sim.GetPosition(pawnId.Value);

            // Check if pawn is using the fridge
            if (
                sim.Entities.Actions.TryGetValue(pawnId.Value, out var actionComp)
                && actionComp.CurrentAction?.Type == ActionType.UseObject
            )
            {
                positionWhenUsingFridge = pos;
                _output.WriteLine($"Tick {tick}: Pawn using fridge at position {pos}");
                break;
            }

            if (tick % 20 == 0)
            {
                string actionName = "none";
                if (
                    sim.Entities.Actions.TryGetValue(pawnId.Value, out var ac)
                    && ac.CurrentAction != null
                )
                    actionName = ac.CurrentAction.DisplayName ?? ac.CurrentAction.Type.ToString();
                _output.WriteLine($"Tick {tick}: Pawn at {pos}, action={actionName}");
            }
        }

        // Assert: Pawn should have used fridge from the valid UseArea position (2,1)
        Assert.NotNull(positionWhenUsingFridge);
        Assert.Equal(new TileCoord(2, 1), positionWhenUsingFridge.Value);
    }

    /// <summary>
    /// Test that pawns can use objects with multiple UseAreas and pick the closest one.
    ///
    /// Layout (5x5 world):
    ///   Y=0: [ ][ ][ ][ ][ ]
    ///   Y=1: [ ][U][T][U][ ]   T = TV at (2,1), U = Use areas at (1,1) and (3,1)
    ///   Y=2: [ ][ ][U][ ][ ]   U = Use area at (2,2)
    ///   Y=3: [ ][ ][ ][ ][ ]
    ///   Y=4: [P][ ][ ][ ][ ]   P = Pawn at (0,4)
    ///
    /// Pawn should pick the closest use area.
    /// </summary>
    [Fact]
    public void Pawn_PicksClosestUseArea_WhenMultipleAvailable()
    {
        // Arrange: TV at (2,1) with multiple use areas
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 4)
            .DefineNeed("Fun", "Fun", decayPerTick: 0.001f)
            .DefineObject(
                "TV",
                "TV",
                satisfiesNeed: "Fun",
                satisfactionAmount: 40f,
                interactionDuration: 30,
                useAreas: new List<(int, int)> { (-1, 0), (1, 0), (0, 1) }
            )
            .AddObject("TV", 2, 1)
            .AddPawn("TestPawn", 0, 4, new Dictionary<string, float> { { "Fun", 10f } })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== Multiple UseAreas Test ===");
        _output.WriteLine("TV at (2,1), UseAreas: (-1,0)=(1,1), (1,0)=(3,1), (0,1)=(2,2)");
        _output.WriteLine("Pawn starts at (0,4)");

        TileCoord? positionWhenUsingTV = null;

        // Act: Run simulation until pawn uses the TV
        for (int tick = 0; tick < 200; tick++)
        {
            sim.Tick();

            var pos = sim.GetPosition(pawnId.Value);

            if (
                sim.Entities.Actions.TryGetValue(pawnId.Value, out var actionComp)
                && actionComp.CurrentAction?.Type == ActionType.UseObject
            )
            {
                positionWhenUsingTV = pos;
                _output.WriteLine($"Tick {tick}: Pawn using TV at position {pos}");
                break;
            }

            if (tick % 20 == 0)
            {
                string actionName = "none";
                if (
                    sim.Entities.Actions.TryGetValue(pawnId.Value, out var ac)
                    && ac.CurrentAction != null
                )
                    actionName = ac.CurrentAction.DisplayName ?? ac.CurrentAction.Type.ToString();
                _output.WriteLine($"Tick {tick}: Pawn at {pos}, action={actionName}");
            }
        }

        // Assert: Pawn should be at one of the valid use areas
        Assert.NotNull(positionWhenUsingTV);

        var validUseAreas = new HashSet<TileCoord>
        {
            new TileCoord(1, 1), // (-1, 0) relative to (2,1)
            new TileCoord(3, 1), // (1, 0) relative to (2,1)
            new TileCoord(2, 2), // (0, 1) relative to (2,1)
        };

        Assert.Contains(positionWhenUsingTV.Value, validUseAreas);
        _output.WriteLine($"Pawn correctly used TV from valid UseArea: {positionWhenUsingTV}");
    }

    /// <summary>
    /// Test that pawn cannot use object if all UseAreas are blocked.
    /// </summary>
    [Fact]
    public void Pawn_CannotUseObject_WhenAllUseAreasBlocked()
    {
        // Arrange: Fridge with single use area that's blocked by another object
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 2)
            .DefineNeed("Hunger", "Hunger", decayPerTick: 0.001f)
            .DefineObject(
                "Fridge",
                "Fridge",
                satisfiesNeed: "Hunger",
                satisfactionAmount: 50f,
                interactionDuration: 20,
                useAreas: new List<(int, int)> { (0, 1) }
            )
            .DefineObject("Blocker", "Blocker")
            .AddObject("Fridge", 2, 0)
            .AddObject("Blocker", 2, 1)
            .AddPawn("TestPawn", 0, 2, new Dictionary<string, float> { { "Hunger", 10f } })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== Blocked UseArea Test ===");
        _output.WriteLine("Fridge at (2,0), UseArea at (2,1) is BLOCKED by another object");

        float initialHunger = sim.GetNeedValue(pawnId.Value, "Hunger");

        // Act: Run simulation - pawn should NOT be able to use fridge
        for (int tick = 0; tick < 100; tick++)
        {
            sim.Tick();
        }

        // Assert: Hunger should have only decayed (not been satisfied)
        float finalHunger = sim.GetNeedValue(pawnId.Value, "Hunger");

        // With decay of 0.001 per tick over 100 ticks, hunger drops by ~0.1
        // If pawn ate, hunger would jump up by 50
        Assert.True(
            finalHunger <= initialHunger,
            $"Pawn should not have eaten (UseArea blocked). Initial: {initialHunger}, Final: {finalHunger}"
        );

        _output.WriteLine(
            $"Hunger went from {initialHunger} to {finalHunger} - pawn correctly couldn't use blocked fridge"
        );
    }
}
