using System;
using System.Collections.Generic;
using System.Linq;
using Arch;
using Arch.Core;
using Arch.Core.Extensions;
using ArchTest.Components;
using Arch.System;

// https://github.com/genaray/Arch

namespace ArchTest.Components
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

namespace ArchTest
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
            return world.Create(
                new Tile(), 
                new Location { TileIndex = tileIndex }, 
                new StoresFood());
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
            return world.Create(
                new Location { TileIndex = tileIndex },
                new Populated { NumberOfPeople = population }
                );
        }

        /// <summary>
        /// Create a Farm
        /// </summary>
        /// <param name="world"></param>
        /// <param name="tileIndex"></param>
        /// <returns></returns>
        public static Entity CreateFarm(this World world, int tileIndex)
        {
            return world.Create(
                new Location { TileIndex = tileIndex },
                new ProducesFood { FoodProducedPerTick = 10 }
                );
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
            return context.Create(
                new StoresFood { FoodStored = 0 },
                new Location { TileIndex = startIndex },
                new Shipment { DestinationTileIndex = destinationIndex }
                );
        }

        /// <summary>
        /// Create board.
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public static Entity CreateBoard(this World world, WorldSize size)
        {
            return world.Create(new Board { Width = (int)size, Height = (int)size });
        }

        public static Entity GetBoard(this World world)
        {
            // TODO: slow
            throw new NotImplementedException("Not done");
            List<Entity> buf = new();
            world.GetEntities(new QueryDescription().WithAll<Board>(), buf);
            return buf[0];
        }

        public static Entity GetTileAt(this World world, in Location location)
        {
            // TODO: implement
            throw new NotImplementedException("Not done");
            return default;
        }

        public static double DistanceBetween(this World context, Entity a, Entity b)
        {
            if (!a.Has<Location>()) return double.MaxValue;
            if (!b.Has<Location>()) return double.MaxValue;
            ref var posA = ref a.Get<Location>();
            ref var posB = ref b.Get<Location>();
            var boardEntity = context.GetBoard();
            ref var board = ref boardEntity.Get<Board>();
            var (x0, y0) = board.PositionFromTileIndex(posA.TileIndex);
            var (x1, y1) = board.PositionFromTileIndex(posB.TileIndex);
            var dx = x0 - x1;
            var dy = y0 - y1;
            var distanceSquared = (dx * dx + dy * dy);
            if (distanceSquared == 0) return 0;
            return Math.Sqrt(distanceSquared);
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

    class MyBaseSystem : BaseSystem<World, float>
    {
        public MyBaseSystem(World w) : base(w)
        {

        }

        public QueryDescription Q()
        {
            return new QueryDescription();
        }
    }

    /// <summary>
    /// System to consume food.
    /// </summary>
    class SystemConsumeFood : MyBaseSystem
    {
        public SystemConsumeFood(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            var query = new QueryDescription()
                .WithAll<ConsumesFood, Location>();

            World.Query(query, (ref ConsumesFood consumes, ref Location location) =>
            {
                var tileEntity = World.GetTileAt(location);
                ref var store = ref tileEntity.Get<StoresFood>();
                int foodToConsume = consumes.FoodConsumedPerTick;
                store.FoodStored -= foodToConsume;
                foodToConsume = Math.Max(0, -store.FoodStored);
                store.FoodStored = Math.Max(0, store.FoodStored);
                consumes.DemandUnsatisfiedLastTick = foodToConsume;
            });

        }
    }

    /// <summary>
    /// System to produce food.
    /// </summary>
    class SystemProduceFood : MyBaseSystem
    {
        public SystemProduceFood(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            var query = Q().WithAll<ProducesFood, Location>();

            World.Query(query, (ref ProducesFood farm, ref Location location) =>
            {
                var tileEntity = World.GetTileAt(location);
                ref var store = ref tileEntity.Get<StoresFood>();
                store.FoodStored += farm.FoodProducedPerTick;
            });
        }
    }

    /// <summary>
    /// System to manage population.
    /// </summary>
    class SystemPopulation : MyBaseSystem
    {
        // TODO: CAN WE ADD COMPONENTS AND DELETE ENTITIES DURING ITERATION?

        public SystemPopulation(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            // Add food consuming component to population.
            World.Query(Q().WithAll<Populated>().WithNone<ConsumesFood>(), (in Entity entity, ref Populated populated) =>
            {
                entity.Add(new ConsumesFood());
            });

            // Update population based on unmet demand
            // Update demand based on population
            World.Query(Q().WithAll<Populated, ConsumesFood>(), (ref Populated population, ref ConsumesFood consumes) =>
            {
                population.NumberOfPeople = Math.Max(population.NumberOfPeople - consumes.DemandUnsatisfiedLastTick, 0);
                consumes.FoodConsumedPerTick = population.NumberOfPeople;
            });

            // Destroy population entities if there are no people left.
            World.Query(Q().WithAll<Populated>(), (in Entity entity, ref Populated populated) =>
            {
                if (populated.NumberOfPeople <= 0)
                {
                    World.Destroy(entity);
                }
            });
        }
    }

    /// <summary>
    /// Try to ship food to where it is needed.
    /// </summary>
    class SystemMakeShipments : MyBaseSystem
    { 

        // TODO: CAN WE ADD COMPONENTS DURING ITERATION?

        // TODO: Efficiency?

        public SystemMakeShipments(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            var producerEntities = new List<Entity>();
            World.GetEntities(Q().WithAll<ProducesFood, Location>(), producerEntities);
            World.Query(Q().WithAll<ConsumesFood, Location>(), (in Entity consumerEntity, ref ConsumesFood consumer, ref Location consumerLocation) =>
            {
                if (consumer.DemandUnsatisfiedLastTick > 0)
                {
                    var consumerEntityCopy = consumerEntity;
                    var demand = consumer.FoodConsumedPerTick;
                    var orderedProducers = producerEntities.OrderByDescending(x => World.DistanceBetween(x, consumerEntityCopy));
                    foreach (var producerEntity in orderedProducers)
                    {
                        if (demand == 0) break;
                        ref var producerLocation = ref producerEntity.Get<Location>();
                        var producerTileEntity = World.GetTileAt(producerLocation);
                        ref var store = ref producerTileEntity.Get<StoresFood>();
                        var shipped = demand;
                        store.FoodStored -= demand;
                        demand = Math.Max(0, -store.FoodStored);
                        shipped -= demand;
                        store.FoodStored = Math.Max(store.FoodStored, 0);
                        World.CreateShipment(shipped, producerLocation.TileIndex, consumerLocation.TileIndex);
                    }
                }
            });
        }
    }

    /// <summary>
    /// Compute paths for all shipments that need them. This can be
    /// expensive, so we could bail out early if too much time has
    /// been spent this frame. At the moment though all paths that need
    /// calculating will be done right now.
    /// </summary>
    class SystemPrepareShipments : MyBaseSystem
    {
        public SystemPrepareShipments(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            var query = Q().WithAll<Shipment, Location>().WithNone<Path>();
            World.Query(query, (in Entity entity, ref Shipment shipment, ref Location location) =>
            {
                entity.Set(new Path
                {
                    TileIndices = GetPath(location.TileIndex, shipment.DestinationTileIndex),
                    Current = 0
                });
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
            var boardEntity = World.GetBoard();
            var board = boardEntity.Get<Board>(); // note copy for lambda.
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
    class SystemAdvanceShipments : MyBaseSystem
    {

        // TODO: CAN WE ADD AND REMOVE COMPONENTS DURING ITERATION?

        // TODO: Efficiency?

        public SystemAdvanceShipments(World world) : base(world)
        {
        }

        public override void Update(in float elapsedTime)
        {
            var query = Q().WithAll<Shipment, Location, Path, StoresFood>();
            World.Query(query, (in Entity entity,
                                ref Shipment shipment,
                                ref Location position,
                                ref Path path,
                                ref StoresFood store) =>
            {
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
                    var tileEntity = World.GetTileAt(position);
                    var tileInventory = tileEntity.Get<StoresFood>();
                    var shipmentInventory = entity.Get<StoresFood>();
                    tileInventory.FoodStored += shipmentInventory.FoodStored;
                    World.Destroy(entity);
                    return;
                }

                // Advance. Note that this depends on the path being
                // a correct representation of tile neighbours or the
                // shipment will simply teleport!
                position.TileIndex = path.TileIndices[path.Current++];
            });
        }
    }

    /// <summary>
    /// The program.
    /// </summary>
    class Program
    {
        public static void Initialize(World _context)
        {
            var boardEntity = _context.CreateBoard(WorldSize.Typical);
            ref var board = ref boardEntity.Get<Board>();
            for (int i = 0; i < board.Width * board.Height; ++i)
            {
                Entity tileEntity = _context.CreateTile(i);
            }
        }

        public static void Main()
        {
            // note: 'float' as type parameter here means time in ms.

            // Initialise
            var world = World.Create();
            Initialize(world);

            // Create systems
            var systems = new Group<float>(
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