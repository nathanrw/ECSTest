using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;
using DefaultEcs.System;
using DefaultTest.Components;

// https://github.com/Doraku/DefaultEcs

namespace DefaultTest.Components
{
    /// <summary>
    /// Determines whether tile is navigable.
    /// </summary>
    enum TileTerrain
    {
        Water,
        Land,
        Mountains
    }

    /// <summary>
    /// Details about a tile.
    /// </summary>
    struct Tile
    {
        public TileTerrain Terrain;
    }

    struct ConsumesFood
    {
        public int FoodConsumedPerTick;
        public int DemandUnsatisfiedLastTick;
    }

    struct ProducesFood
    {
        public int FoodProducedPerTick;
    }

    struct StoresFood
    {
        public int FoodStored;
    }

    struct Populated
    {
        public int NumberOfPeople;
    }

    /// <summary>
    /// Marks entity as having a tile position.
    /// </summary>
    struct Location
    {
        public int TileIndex;
    }

    /// <summary>
    /// Going somewhere
    /// </summary>
    struct Shipment
    {
        public int DestinationTileIndex;
    }

    struct Path
    {
        public int Current;
        public int[] TileIndices;
    }

    struct Board
    {
        public int Width;
        public int Height;

        /// <summary>
        /// XY position from index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public (int, int) PositionFromTileIndex(int index)
        {
            return (index % Width, index / Width);
        }

        /// <summary>
        /// Get a tile index from a position.
        /// Returns -1 if invalid position.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int TileIndexFromPosition(int x, int y)
        {
            if (x < 0 || x >= Width) return -1;
            if (y < 0 || y >= Height) return -1;
            return x + y * Width;
        }

        /// <summary>
        /// Get neighbours of (square) tile.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int[] GetTileNeighbours(int index)
        {
            var (x, y) = PositionFromTileIndex(index);
            int[] ret =
            {
                TileIndexFromPosition(x-1, y),
                TileIndexFromPosition(x-1, y-1),
                TileIndexFromPosition(x+1, y),
                TileIndexFromPosition(x+1, y+1)
            };
            return ret.Where(x => x != -1).ToArray();
        }

        /// <summary>
        /// Decide elevation based on distance from centre.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public TileTerrain DecideTerrain(int index)
        {
            var (x, y) = PositionFromTileIndex(index);
            double xN = (Width / 2 - x) / (double)Width;
            double yN = (Height / 2 - y) / (double)Height;
            double distFromCentreNormalizedSquared = xN * xN + yN * yN;
            double factor = 1 - distFromCentreNormalizedSquared == 0 ? 0 : Math.Sqrt(distFromCentreNormalizedSquared);
            if (factor < 0.1) return TileTerrain.Water;
            if (factor < 0.9) return TileTerrain.Land;
            return TileTerrain.Mountains;
        }
    }
}

namespace DefaultTest
{

    /// <summary>
    /// Methods to create and index entities.
    /// </summary>
    static class MyWorldExtensionMethods
    {
        /// <summary>
        /// Create a Tile.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="tileIndex"></param>
        /// <returns></returns>
        public static Entity CreateTile(this World world, int tileIndex)
        {
            var entity = world.CreateEntity();
            entity.Set(new Tile());
            entity.Set(new Location { TileIndex = tileIndex });
            entity.Set(new StoresFood { FoodStored = 0 });
            return entity;
        }

        /// <summary>
        /// Create a City
        /// </summary>
        /// <param name="world"></param>
        /// <param name="tileIndex"></param>
        /// <param name="population"></param>
        /// <returns></returns>
        public static Entity CreateCity(this World world, int tileIndex, int population)
        {
            var entity = world.CreateEntity();
            entity.Set(new Location { TileIndex = tileIndex });
            entity.Set(new Populated { NumberOfPeople = population });
            return entity;
        }

        /// <summary>
        /// Create a Farm
        /// </summary>
        /// <param name="world"></param>
        /// <param name="tileIndex"></param>
        /// <returns></returns>
        public static Entity CreateFarm(this World world, int tileIndex)
        {
            var entity = world.CreateEntity();
            entity.Set(new Location { TileIndex = tileIndex });
            entity.Set(new ProducesFood { FoodProducedPerTick = 10 });
            return entity;
        }

        /// <summary>
        /// Create a shipment to a destination.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="amountOfFood"></param>
        /// <param name="startIndex"></param>
        /// <param name="destinationIndex"></param>
        /// <returns></returns>
        public static Entity CreateShipment(
            this World context,
            int amountOfFood,
            int startIndex,
            int destinationIndex
        )
        {
            var entity = context.CreateEntity();
            entity.Set(new StoresFood { FoodStored = 0 });
            entity.Set(new Location { TileIndex = startIndex });
            entity.Set(new Shipment { DestinationTileIndex = destinationIndex });
            return entity;
        }

        /// <summary>
        /// Create board.
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public static ref Board CreateBoard(this World world, WorldSize size)
        {
            // Default lets you add components directly to the world
            world.Set(new Board { Width = (int)size, Height = (int)size });
            return ref world.Get<Board>();
        }

        public static ref Board GetBoard(this World world)
        {
            return ref world.Get<Board>();
        }

        public static double DistanceBetween(this World context, Entity a, Entity b)
        {
            if (!a.Has<Location>()) return double.MaxValue;
            if (!b.Has<Location>()) return double.MaxValue;
            ref var posA = ref a.Get<Location>();
            ref var posB = ref b.Get<Location>();
            ref var board = ref context.GetBoard();
            var (x0, y0) = board.PositionFromTileIndex(posA.TileIndex);
            var (x1, y1) = board.PositionFromTileIndex(posB.TileIndex);
            var dx = x0 - x1;
            var dy = y0 - y1;
            var distanceSquared = (dx * dx + dy * dy);
            if (distanceSquared == 0) return 0;
            return Math.Sqrt(distanceSquared);
        }

        public static EntityMap<Location> CreateLocationMap(this World context)
        {
            return context.GetEntities()
                .With<Location>()
                .With<Tile>()
                .AsMap<Location>();
        }
    }

    /// <summary>
    /// Determines number of tiles.
    /// </summary>
    enum WorldSize
    {
        Small = 32,
        Typical = 128,
        Large = 512,
        VeryLarge = 1024
    }

    /// <summary>
    /// System to consume food.
    /// </summary>
    class SystemConsumeFood : AEntitySetSystem<float>
    {
        private World _world;
        private EntityMap<Location> _locationMap;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<ConsumesFood>()
                .With<Location>()
                .AsSet();
        }

        public SystemConsumeFood(World world) : base(MakeSet(world))
        {
            _world = world;
            _locationMap = world.CreateLocationMap();
        }

        protected override void Update(float elapsedTime, in Entity e)
        {
            ref var consumes = ref e.Get<ConsumesFood>();
            ref var location = ref e.Get<Location>();
            var tileEntity = _locationMap[location];
            ref var store = ref tileEntity.Get<StoresFood>();
            int foodToConsume = consumes.FoodConsumedPerTick;
            store.FoodStored -= foodToConsume;
            foodToConsume = Math.Max(0, -store.FoodStored);
            store.FoodStored = Math.Max(0, store.FoodStored);
            consumes.DemandUnsatisfiedLastTick = foodToConsume;
        }
    }

    /// <summary>
    /// System to produce food.
    /// </summary>
    class SystemProduceFood : AEntitySetSystem<float>
    {
        private World _world;
        private EntityMap<Location> _locationMap;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<ProducesFood>()
                .With<Location>()
                .AsSet();
        }

        public SystemProduceFood(World world) : base(MakeSet(world))
        {
            _world = world;
            _locationMap = world.CreateLocationMap();
        }

        protected override void Update(float elapsedTime, in Entity e)
        {
            var tileEntity = _locationMap[e.Get<Location>()];
            ref var store = ref tileEntity.Get<StoresFood>();
            ref var farm = ref e.Get<ProducesFood>();
            store.FoodStored += farm.FoodProducedPerTick;
        }
    }

    /// <summary>
    /// System to manage population.
    /// </summary>
    class SystemPopulation : AEntitySetSystem<float>
    {
        private World _world;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<Populated>()
                .With<Location>()
                .AsSet();
        }
        
        // TODO: CAN WE ADD COMPONENTS DURING ITERATION?

        public SystemPopulation(World world) : base(MakeSet(world), true)
        {
            _world = world;
        }

        protected override void Update(float elapsedTime, in Entity e)
        {
            ref var population = ref e.Get<Populated>();
            if (population.NumberOfPeople > 0)
            {
                if (!e.Has<ConsumesFood>())
                {
                    e.Set(new ConsumesFood());
                }
                ref var consumes = ref e.Get<ConsumesFood>();
                population.NumberOfPeople = Math.Max(population.NumberOfPeople - consumes.DemandUnsatisfiedLastTick, 0);
                consumes.FoodConsumedPerTick = population.NumberOfPeople;
                if (population.NumberOfPeople == 0)
                {
                    e.Remove<ConsumesFood>();
                }
            }
        }
    }

    /// <summary>
    /// Try to ship food to where it is needed.
    /// </summary>
    class SystemMakeShipments : AEntitySetSystem<float>
    {
        private World _world;
        private EntityMap<Location> _locationMap;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<ProducesFood>()
                .With<Location>()
                .AsSet();
        }

        // TODO: CAN WE ADD COMPONENTS DURING ITERATION?

        // TODO: Efficiency?

        public SystemMakeShipments(World world) : base(MakeSet(world), true)
        {
            _world = world;
            _locationMap = world.CreateLocationMap();
        }

        protected override void Update(float elapsedTime, in Entity consumerEntity)
        {
            var producerEntities = _world.GetEntities().With<ProducesFood>().With<Location>().AsEnumerable();
            ref var consumer = ref consumerEntity.Get<ConsumesFood>();
            ref var consumerLocation = ref consumerEntity.Get<Location>();
            if (consumer.DemandUnsatisfiedLastTick > 0)
            {
                var consumerEntityCopy = consumerEntity; // avoid use of ref in lambda.
                var demand = consumer.FoodConsumedPerTick;
                var orderedProducers = producerEntities.OrderByDescending(x => _world.DistanceBetween(x, consumerEntityCopy));
                foreach (var producerEntity in orderedProducers)
                {
                    if (demand == 0) break;
                    var producerTileEntity = _locationMap[producerEntity.Get<Location>()];
                    ref var store = ref producerTileEntity.Get<StoresFood>();
                    ref var producerLocation = ref producerEntity.Get<Location>();
                    var shipped = demand;
                    store.FoodStored -= demand;
                    demand = Math.Max(0, -store.FoodStored);
                    shipped -= demand;
                    store.FoodStored = Math.Max(store.FoodStored, 0);
                    _world.CreateShipment(shipped, producerLocation.TileIndex, consumerLocation.TileIndex);
                }
            }
        }
    }

    /// <summary>
    /// Compute paths for all shipments that need them. This can be
    /// expensive, so we could bail out early if too much time has
    /// been spent this frame. At the moment though all paths that need
    /// calculating will be done right now.
    /// </summary>
    class SystemPrepareShipments : AEntitySetSystem<float>
    {
        private World _world;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<Shipment>()
                .With<Location>()
                .Without<Path>()
                .AsSet();
        }

        // TODO: CAN WE ADD COMPONENTS DURING ITERATION?

        // TODO: Efficiency?

        public SystemPrepareShipments(World world) : base(MakeSet(world), true)
        {
            _world = world;
        }

        protected override void Update(float elapsedTime, in Entity entity)
        {
            ref var position = ref entity.Get<Location>();
            ref var shipment = ref entity.Get<Shipment>();
            entity.Set(new Path {
                TileIndices = GetPath(position.TileIndex, shipment.DestinationTileIndex),
                Current = 0
            });
        }

        /// <summary>
        /// Compute a path with A*.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="finish"></param>
        /// <returns></returns>
        private int[] GetPath(int start, int finish)
        {
            var board = _world.GetBoard();
            var astar = new AStar<int>(
                -1,
                x => board.GetTileNeighbours(x),
                (x, y) => 1, // Weight
                (x, y) => 1  // Heuristic
            );
            return astar.GetPath(start, finish).ToArray();
        }
    }

    /// <summary>
    /// Advance each shipment along its path.
    /// </summary>
    class SystemAdvanceShipments : AEntitySetSystem<float>
    {
        private World _world;
        private EntityMap<Location> _locationMap;

        private static EntitySet MakeSet(World world)
        {
            return world.GetEntities()
                .With<Shipment>()
                .With<Location>()
                .With<Path>()
                .With<StoresFood>()
                .AsSet();
        }

        // TODO: CAN WE ADD COMPONENTS DURING ITERATION?

        // TODO: Efficiency?

        public SystemAdvanceShipments(World world) : base(MakeSet(world), true)
        {
            _world = world;
            _locationMap = world.CreateLocationMap();
        }

        protected override void Update(float elapsedTime, in Entity entity)
        {
            ref var position = ref entity.Get<Location>();
            ref var path = ref entity.Get<Path>();
            ref var shipment = ref entity.Get<Shipment>();

            // Check for lost shipments.
            if (path.TileIndices[path.Current] != position.TileIndex ||
                path.TileIndices.Last() != shipment.DestinationTileIndex)
            {
                entity.Remove<Path>();
                return;
            }

            // Check for arrival
            if (position.TileIndex == shipment.DestinationTileIndex)
            {
                var tileEntity = _locationMap[position];
                var tileInventory = tileEntity.Get<StoresFood>();
                var shipmentInventory = entity.Get<StoresFood>();
                tileInventory.FoodStored += shipmentInventory.FoodStored;
                entity.Dispose();
                return;
            }

            // Advance. Note that this depends on the path being
            // a correct representation of tile neighbours or the
            // shipment will simply teleport!
            position.TileIndex = path.TileIndices[path.Current++];
        }
    }

    /// <summary>
    /// The program.
    /// </summary>
    class Program
    {
        public static void Initialize(World _context)
        {
            ref var board = ref _context.CreateBoard(WorldSize.Typical);
            for (int i = 0; i < board.Width * board.Height; ++i)
            {
                Entity tileEntity = _context.CreateTile(i);
            }
        }

        public static void Main()
        {
            // note: 'float' as type parameter here means time in ms.

            // Initialise
            var world = new World();
            Initialize(world);

            // Create systems
            var systems = new SequentialSystem<float>(
                new SystemPopulation(world),
                new SystemProduceFood(world),
                new SystemConsumeFood(world),
                new SystemMakeShipments(world),
                new SystemPrepareShipments(world),
                new SystemAdvanceShipments(world)
            );

            // Update loop
            const float dt = 1;
            for (int i = 0; i < 100; ++i)
            {
                systems.Update(dt);
            }
        }
    }
}