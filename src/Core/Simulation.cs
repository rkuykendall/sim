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
        BootstrapPawns();
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

    private void BootstrapPawns()
    {
        // Pawn data: (name, age, x, y, hunger, energy, fun, social, comfort, hygiene)
        var pawnData = new[]
        {
            ("Alex", 25, 5, 5, 70f, 60f, 50f, 80f, 70f, 65f),
            ("Jordan", 32, 3, 7, 55f, 75f, 40f, 60f, 80f, 70f),
            ("Sam", 28, 9, 3, 80f, 45f, 65f, 50f, 55f, 85f),
            ("Riley", 22, 7, 9, 60f, 55f, 75f, 70f, 45f, 50f),
        };

        foreach (var (name, age, x, y, hunger, energy, fun, social, comfort, hygiene) in pawnData)
        {
            var id = Entities.Create();

            Entities.Pawns[id] = new PawnComponent { Name = name, Age = age };
            Entities.Positions[id] = new PositionComponent { Coord = new TileCoord(x, y) };
            Entities.Moods[id] = new MoodComponent { Mood = 0 };
            Entities.Needs[id] = new NeedsComponent
            {
                Needs = new Dictionary<int, float>
                {
                    { ContentDatabase.NeedHunger, hunger },
                    { ContentDatabase.NeedEnergy, energy },
                    { ContentDatabase.NeedFun, fun },
                    { ContentDatabase.NeedSocial, social },
                    { ContentDatabase.NeedComfort, comfort },
                    { ContentDatabase.NeedHygiene, hygiene }
                }
            };
            Entities.Buffs[id] = new BuffComponent();
            Entities.Actions[id] = new ActionComponent();
        }
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
