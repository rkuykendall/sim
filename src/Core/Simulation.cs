using System.Collections.Generic;

namespace SimGame.Core;

public sealed class Simulation
{
    public const int TickRate = 20;

    public World World { get; } = new();
    public EntityManager Entities { get; } = new();
    public TimeService Time { get; } = new();

    private readonly SystemManager _systems = new();

    public Simulation()
    {
        _systems.Add(new NeedsSystem());
        _systems.Add(new BuffSystem());
        _systems.Add(new MoodSystem());
        _systems.Add(new ActionSystem());
        _systems.Add(new AISystem());

        BootstrapWorld();
        BootstrapPawn();
    }

    private void BootstrapWorld()
    {
        // Create a fridge
        var fridgeId = Entities.Create();
        Entities.Positions[fridgeId] = new PositionComponent { Coord = new TileCoord(2, 3) };
        Entities.Objects[fridgeId] = new ObjectComponent { ObjectDefId = ContentDatabase.ObjectFridge };
        World.GetTile(new TileCoord(2, 3)).Walkable = false;

        // Create a bed
        var bedId = Entities.Create();
        Entities.Positions[bedId] = new PositionComponent { Coord = new TileCoord(8, 8) };
        Entities.Objects[bedId] = new ObjectComponent { ObjectDefId = ContentDatabase.ObjectBed };
        World.GetTile(new TileCoord(8, 8)).Walkable = false;

        // Create a TV for fun
        var tvId = Entities.Create();
        Entities.Positions[tvId] = new PositionComponent { Coord = new TileCoord(6, 2) };
        Entities.Objects[tvId] = new ObjectComponent { ObjectDefId = ContentDatabase.ObjectTV };
        World.GetTile(new TileCoord(6, 2)).Walkable = false;

        // Create a shower for hygiene
        var showerId = Entities.Create();
        Entities.Positions[showerId] = new PositionComponent { Coord = new TileCoord(10, 5) };
        Entities.Objects[showerId] = new ObjectComponent { ObjectDefId = ContentDatabase.ObjectShower };
        World.GetTile(new TileCoord(10, 5)).Walkable = false;
    }

    private void BootstrapPawn()
    {
        var id = Entities.Create();

        Entities.Pawns[id] = new PawnComponent { Name = "Alex", Age = 25 };
        Entities.Positions[id] = new PositionComponent { Coord = new TileCoord(5, 5) };
        Entities.Moods[id] = new MoodComponent { Mood = 0 };
        Entities.Needs[id] = new NeedsComponent
        {
            Needs = new Dictionary<int, float>
            {
                { ContentDatabase.NeedHunger, 70f },
                { ContentDatabase.NeedEnergy, 60f },
                { ContentDatabase.NeedFun, 50f },
                { ContentDatabase.NeedSocial, 80f },
                { ContentDatabase.NeedComfort, 70f },
                { ContentDatabase.NeedHygiene, 65f }
            }
        };
        Entities.Buffs[id] = new BuffComponent();
        Entities.Actions[id] = new ActionComponent();
    }

    public void Tick()
    {
        Time.AdvanceTick();
        var ctx = new SimContext(this);
        _systems.TickAll(ctx);
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }
}
