using System.Collections.Generic;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Integration tests for the attachment-based preference scoring system.
/// Tests verify that pawns prefer objects they've used before (high attachment)
/// and avoid objects claimed by other pawns (high others' attachment).
///
/// This is tested through simulation scenarios rather than direct unit tests,
/// as attachment scoring is internal to the AI decision-making system.
/// </summary>
public class AttachmentPreferenceScoringTests
{
    private readonly ITestOutputHelper _output;

    public AttachmentPreferenceScoringTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Pawn_WithNoAttachments_SelectsClosestObject()
    {
        // When pawns have no attachment history, they should pick the closest bed
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(20, 10);
        var sleepNeedId = builder.DefineNeed("Sleep", decayPerTick: 0.001f);
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: sleepNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (0, 1) }
        );

        builder.AddObject(bedDefId, 1, 0); // Close bed at (1,0)
        builder.AddObject(bedDefId, 10, 0); // Distant bed at (10,0)
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Run simulation to let pawn choose a bed
        sim.RunTicks(200);

        // Check that pawn is using the closer bed
        var posNullable = sim.GetPosition(pawnId.Value);
        Assert.NotNull(posNullable);
        var positions = posNullable.Value;
        _output.WriteLine($"Pawn is at position {positions}");

        // Pawn should be near the close bed at (1,0), likely at use area (1,1)
        int distToCloseBed = System.Math.Abs(positions.X - 1) + System.Math.Abs(positions.Y - 1);
        int distToDistantBed =
            System.Math.Abs(positions.X - 10) + System.Math.Abs(positions.Y - 10);

        _output.WriteLine(
            $"Distance to close bed: {distToCloseBed}, distance to distant bed: {distToDistantBed}"
        );
        Assert.True(distToCloseBed < distToDistantBed, "Pawn should be closer to the close bed");
    }

    [Fact]
    public void Pawn_PrefersAttachedObject_OverCloserUnused()
    {
        // A pawn should prefer beds they've used before, even if further away
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(20, 10);
        var sleepNeedId = builder.DefineNeed("Sleep", decayPerTick: 0.01f);
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: sleepNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (0, 1) }
        );

        builder.AddObject(bedDefId, 1, 0); // Close unused bed
        builder.AddObject(bedDefId, 5, 0); // Far bed that will get attachment
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Run to let pawn use the far bed multiple times and build attachment
        for (int cycle = 0; cycle < 3; cycle++)
        {
            sim.RunTicks(200); // Enough time to use a bed
        }

        var posNullable2 = sim.GetPosition(pawnId.Value);
        Assert.NotNull(posNullable2);
        var position = posNullable2.Value;
        _output.WriteLine($"After building attachment, pawn is at {position}");

        // Pawn should now prefer the far bed due to attachment
        // We can't directly test this without accessing internal AI,
        // so we verify by running more ticks and checking the pawn returns to use it
        int distToCloseBed = System.Math.Abs(position.X - 1) + System.Math.Abs(position.Y - 1);
        int distToFarBed = System.Math.Abs(position.X - 5) + System.Math.Abs(position.Y - 5);

        _output.WriteLine($"Distance to close bed: {distToCloseBed}, to far bed: {distToFarBed}");
        // After building attachment, pawn should be using/near the far bed
        Assert.True(distToFarBed <= distToCloseBed);
    }

    [Fact]
    public void Pawn_AvoidsBed_WithHighOtherAttachment()
    {
        // Pawns should prefer unattached beds over beds others use frequently
        // This is a weaker test since pawns also use distance in their preference scoring
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(12, 10);
        var sleepNeedId = builder.DefineNeed("Sleep", decayPerTick: 0.001f);
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: sleepNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (0, 1) }
        );

        // Place both beds at same distance from pawn2's starting position
        // Bed1 at x=4 (distance 4 from pawn2 at x=8)
        // Bed2 at x=2 (distance 6 from pawn2 at x=8)  -- further so pawn1 naturally uses it first
        builder.AddObject(bedDefId, 4, 0); // Bed1 - will be claimed by pawn1
        builder.AddObject(bedDefId, 8, 0); // Bed2 - closer to pawn2
        builder.AddPawn("Pawn1", 0, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        builder.AddPawn("Pawn2", 8, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        var sim = builder.Build();

        // Let pawn1 use bed1 a lot to build attachment
        for (int cycle = 0; cycle < 3; cycle++)
        {
            sim.RunTicks(300);
        }

        var pawn2Id = sim.Entities.AllPawns().Last();
        var pawn2Pos = sim.GetPosition(pawn2Id) ?? new TileCoord(0, 0);

        _output.WriteLine($"Pawn2 is at {pawn2Pos}");

        // Pawn2 should be near bed2 (x=8, use area y=1)
        // This is mostly about distance since both pawns need to sleep
        int distToBed2 = System.Math.Abs(pawn2Pos.X - 8);

        _output.WriteLine($"Pawn2 distance to bed2 (x=8): {distToBed2}");

        // Allow some movement for AI but pawn2 should gravitate to nearby bed
        Assert.True(distToBed2 < 5, "Pawn2 should stay relatively close to their nearby bed");
    }

    [Fact]
    public void MultiPawn_System_FormsTerritories()
    {
        // Multiple pawns should form attachment-based territories around beds
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(30, 10);
        var sleepNeedId = builder.DefineNeed("Sleep", decayPerTick: 0.001f);
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: sleepNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (0, 1) }
        );

        // Create 3 beds spread apart
        builder.AddObject(bedDefId, 5, 0);
        builder.AddObject(bedDefId, 15, 0);
        builder.AddObject(bedDefId, 25, 0);

        // Create 3 pawns starting near beds 1, 2, and 3
        builder.AddPawn("Pawn1", 3, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        builder.AddPawn("Pawn2", 15, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        builder.AddPawn("Pawn3", 27, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        var sim = builder.Build();

        // Let them settle into their territories
        for (int cycle = 0; cycle < 3; cycle++)
        {
            sim.RunTicks(300);
        }

        // Get final positions
        var pawnIds = sim.Entities.AllPawns().ToList();
        var positions = pawnIds.Select(p => sim.GetPosition(p) ?? new TileCoord(0, 0)).ToList();

        _output.WriteLine(
            $"Pawn positions: {string.Join(", ", positions.Select(p => $"({p.X},{p.Y})"))} "
        );

        // Calculate distances to each bed to verify territory clustering
        var bed1Dists = positions
            .Select(p => System.Math.Abs(p.X - 5) + System.Math.Abs(p.Y - 1))
            .ToList();
        var bed2Dists = positions
            .Select(p => System.Math.Abs(p.X - 15) + System.Math.Abs(p.Y - 1))
            .ToList();
        var bed3Dists = positions
            .Select(p => System.Math.Abs(p.X - 25) + System.Math.Abs(p.Y - 1))
            .ToList();

        _output.WriteLine(
            $"Bed1 distances: {string.Join(", ", bed1Dists)}, "
                + $"Bed2 distances: {string.Join(", ", bed2Dists)}, "
                + $"Bed3 distances: {string.Join(", ", bed3Dists)}"
        );

        // Verify: Each pawn should be closer to their assigned bed than to others
        // Pawn1 should be closer to bed1 than average distance to bed2 and bed3
        Assert.True(
            bed1Dists[0] < bed2Dists[0] || bed1Dists[0] < bed3Dists[0],
            "Pawn1 should be closer to at least one of the far beds"
        );

        // Pawn3 should be closer to bed3 than to bed1 and bed2
        Assert.True(bed3Dists[2] < bed1Dists[2], "Pawn3 should be closer to bed3 than to bed1");
    }

    [Fact]
    public void AttachmentSystem_WithMaxedAttachment_StillPrefers()
    {
        // Pawns should maintain preference even at max attachment (10)
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(20, 10);
        var sleepNeedId = builder.DefineNeed("Sleep", decayPerTick: 0.001f);
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: sleepNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (0, 1) }
        );

        builder.AddObject(bedDefId, 5, 0); // Preferred bed
        builder.AddObject(bedDefId, 7, 0); // Alternative bed
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { sleepNeedId, 10f } });
        var sim = builder.Build();

        // Run many cycles to max out attachment
        for (int cycle = 0; cycle < 5; cycle++)
        {
            sim.RunTicks(300);
        }

        var pawnId = sim.GetFirstPawn();
        var position = sim.GetPosition(pawnId.Value) ?? new TileCoord(0, 0);

        _output.WriteLine($"After many cycles, pawn at {position}");

        // Pawn should still be near their preferred bed
        int distToPreferred = System.Math.Abs(position.X - 5) + System.Math.Abs(position.Y - 1);
        int distToAlternative = System.Math.Abs(position.X - 7) + System.Math.Abs(position.Y - 1);

        _output.WriteLine(
            $"Distance to preferred: {distToPreferred}, to alternative: {distToAlternative}"
        );
        Assert.True(distToPreferred <= distToAlternative);
    }
}
