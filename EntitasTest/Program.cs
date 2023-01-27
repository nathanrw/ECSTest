using System;
using System.Collections.Generic;
using System.Linq;
using Entitas;
using System.Reflection;

namespace EntitasTest
{
    // n.b. this assumes a single context. but size of entity is determined
    // by number of possible components -- each entity has an array that could
    // hold one of each component. So in the real world will have multiple
    // contexts.

    // also: my static map of component types to ids works with multiple contexts
    // if components are not shared between contexts. perhaps thath is fine.

    // TODO: watched a talk on Entitas that spoke about immutability. rather than
    // mutating components in place i might have to replace them so tha tthe systme
    // knows they have changed

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
    class ComponentTile : IComponent
    {
        public const int TypeId = 0;

        public TileTerrain Terrain = TileTerrain.Land;
    }

    class ComponentConsumesFood : IComponent
    {
        public const int TypeId = 1;

        public int FoodConsumedPerTick = 0;
        public int DemandUnsatisfiedLastTick = 0;
    }

    class ComponentProducesFood : IComponent
    {
        public const int TypeId = 2;

        public int FoodProducedPerTick = 1;
    }

    class ComponentStoresFood : IComponent
    {
        public const int TypeId = 3;

        public int FoodStored = 0;
    }

    class ComponentPopulation : IComponent
    {
        public const int TypeId = 4;

        public int NumberOfPeople = 0;
    }

    /// <summary>
    /// Marks entity as having a tile position.
    /// </summary>
    class ComponentAtTile : IComponent
    {
        public const int TypeId = 5;

        public int TileIndex = -1;
    }

    /// <summary>
    /// Going somewhere
    /// </summary>
    class ComponentShipment : IComponent
    {
        public const int TypeId = 6;

        public int DestinationTileIndex = -1;
    }

    class ComponentPath : IComponent
    {
        public const int TypeId = 7;

        public int Current;
        public int[] TileIndices;
    }

    class ComponentBoard : IComponent
    {
        public const int TypeId = 7;

        public int Width = -1;
        public int Height = -1;

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

    /// <summary>
    /// Static Context factory. Determines types in context.
    /// </summary>
    static class ComponentTypesRegistry
    {
        public static readonly ReflectionUtils.TWithId[] componentTypesWithIds = ReflectionUtils.FindComponentTypes();
        public static readonly Type[] componentTypes = componentTypesWithIds.Select(x => x.T).ToArray();
        public static readonly string[] componentNames = componentTypes.Select(x => x.Name).ToArray();

        public static ContextInfo MakeContextInfo()
        {
            return new("MyContextInfo", componentNames, componentTypes);
        }

        public static IAERC MakeAERC(IEntity entity)
        {
#if (ENTITAS_FAST_AND_UNSAFE)
            return new UnsafeAERC();
#else
            return new SafeAERC(entity);
#endif
        }

        public static Context<Entity> MakeContext()
        {
            const int startCreationIndex = 0;
            var ret = new Context<Entity>(
                componentTypes.Length,
                startCreationIndex,
                MakeContextInfo(),
                MakeAERC,
                () => new Entity()
            );
            ret.CreatePositionIndex();
            return ret;
        }
    }

    /// <summary>
    /// System base class that holds onto a context.
    /// </summary>
    class MySystemBase : ISystem
    {
        protected Context<Entity> _context;

        public MySystemBase()
        {
        }

        public void SetContext(Context<Entity> ctx)
        {
            _context = ctx;
        }
    }

    /// <summary>
    /// Container of systems that initialises them on Add().
    /// </summary>
    class MySystems : Systems
    {
        private Context<Entity> _context;

        public MySystems(Context<Entity> context) : base() { _context = context;  }

        public void Add(MySystemBase system)
        {
            system.SetContext(_context);
            base.Add(system);
        }
    }

    /// <summary>
    /// Methods to create and index entities.
    /// </summary>
    static class MyContextExtensionMethods
    {
        /// <summary>
        /// Create a Tile.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="tileIndex"></param>
        /// <returns></returns>
        public static Entity CreateTile(this Context<Entity> context, int tileIndex)
        {
            var entity = context.CreateEntity();
            var atTile = entity.GetOrAddComponent<ComponentAtTile>();
            var tile = entity.GetOrAddComponent<ComponentTile>();
            var store = entity.GetOrAddComponent<ComponentStoresFood>();
            atTile.TileIndex = tileIndex;
            return entity;
        }

        /// <summary>
        /// Create a City
        /// </summary>
        /// <param name="context"></param>
        /// <param name="tileIndex"></param>
        /// <param name="population"></param>
        /// <returns></returns>
        public static Entity CreateCity(this Context<Entity> context, int tileIndex, int population)
        {
            var entity = context.CreateEntity();
            var atTile = entity.GetOrAddComponent<ComponentAtTile>();
            var people = entity.GetOrAddComponent<ComponentPopulation>();
            atTile.TileIndex = tileIndex;
            people.NumberOfPeople = population;
            return entity;
        }

        /// <summary>
        /// Create a Farm
        /// </summary>
        /// <param name="context"></param>
        /// <param name="tileIndex"></param>
        /// <returns></returns>
        public static Entity CreateFarm(this Context<Entity> context, int tileIndex)
        {
            var entity = context.CreateEntity();
            var atTile = entity.GetOrAddComponent<ComponentAtTile>();
            var createsFood = entity.GetOrAddComponent<ComponentProducesFood>();
            atTile.TileIndex = tileIndex;
            createsFood.FoodProducedPerTick = 10;
            return entity;
        }

        /// <summary>
        /// Create board.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Entity CreateBoard(this Context<Entity> context, WorldSize size)
        {
            var entity = context.CreateEntity();
            var board = entity.GetOrAddComponent<ComponentBoard>();
            board.Width = (int)size;
            board.Height = (int)size;
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
            this Context<Entity> context,
            int amountOfFood,
            int startIndex,
            int destinationIndex
        )
        {
            var entity = context.CreateEntity();
            var food = entity.GetOrAddComponent<ComponentStoresFood>();
            food.FoodStored = amountOfFood;
            var tile = entity.GetOrAddComponent<ComponentAtTile>();
            tile.TileIndex = startIndex;
            var shipment = entity.GetOrAddComponent<ComponentShipment>();
            shipment.DestinationTileIndex = destinationIndex;
            return entity;
        }

        public static Entity GetBoardEntity(this Context<Entity> context)
        {
            return context.GetEntities(Matcher<Entity>.AnyOf(ComponentBoard.TypeId)).First();
        }

        public static double DistanceBetween(this Context<Entity> context, Entity a, Entity b)
        {
            if (!a.HasComponent<ComponentAtTile>()) return double.MaxValue;
            if (!b.HasComponent<ComponentAtTile>()) return double.MaxValue;
            var posA = a.GetComponent<ComponentAtTile>();
            var posB = b.GetComponent<ComponentAtTile>();
            var boardEntity = context.GetBoardEntity();
            var board = boardEntity.GetComponent<ComponentBoard>();
            var (x0, y0) = board.PositionFromTileIndex(posA.TileIndex);
            var (x1, y1) = board.PositionFromTileIndex(posB.TileIndex);
            var dx = x0 - x1;
            var dy = y0 - y1;
            var distanceSquared = (dx * dx + dy * dy);
            if (distanceSquared == 0) return 0;
            return Math.Sqrt(distanceSquared);
        }

        /// <summary>
        /// Unique name for tile position index.
        /// </summary>
        private const string TilePositionIndexName = "TilePositionIndex";
        private const string TileIndexName = "TileIndex";

        // Using indices is the recommended way to associated components, rather than by
        // storing references to entities.  What you see here is normally hidden under
        // code generation.

        public static void CreateTileIndex(this Context<Entity> context)
        {
            var group = context.GetGroup(Matcher<Entity>.AllOf(ComponentAtTile.TypeId, ComponentTile.TypeId));
            var index = new PrimaryEntityIndex<Entity, int>(TileIndexName, group, (e, c) => e.GetComponent<ComponentAtTile>().TileIndex);
            context.AddEntityIndex(index);
        }

        /// <summary>
        /// Get the tile index from the context.
        /// 
        /// CreateTileIndex() must have been called previously.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static PrimaryEntityIndex<Entity, int> GetTileIndex(this Context<Entity> context)
        {
            return context.GetEntityIndex(TileIndexName) as PrimaryEntityIndex<Entity, int>;
        }

        /// <summary>
        /// Create a tile position index for the context.
        /// </summary>
        /// <param name="context"></param>
        public static void CreatePositionIndex(this Context<Entity> context)
        {
            var group = context.GetGroup(Matcher<Entity>.AllOf(ComponentAtTile.TypeId));
            var index = new EntityIndex<Entity, int>(TilePositionIndexName, group, (e, c) => (c as ComponentAtTile).TileIndex);
            context.AddEntityIndex(index);
        }

        /// <summary>
        /// Get the tile index from the context.
        /// 
        /// CreateTileIndex() must have been called previously.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static EntityIndex<Entity, int> GetPositionIndex(this Context<Entity> context)
        {
            return context.GetEntityIndex(TilePositionIndexName) as EntityIndex<Entity, int>;
        }
    }

    /// <summary>
    /// Methods to query entities.
    /// </summary>
    static class MyEntityExtensionMethods
    {
        /// <summary>
        /// Get the tile that an entity is on.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Entity GetTile(this Entity entity, Context<Entity> context)
        {
            if (!entity.HasComponent<ComponentAtTile>()) return null;
            var atTile = entity.GetComponent<ComponentAtTile>();
            var index = context.GetTileIndex();
            return index.GetEntity(atTile.TileIndex);
        }

        /// <summary>
        /// Get all other entities with the same position as the given one.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static HashSet<Entity> GetColocatedEntities(this Entity entity, Context<Entity> context)
        {
            if (!entity.HasComponent<ComponentAtTile>()) return new();
            var atTile = entity.GetComponent<ComponentAtTile>();
            var index = context.GetPositionIndex();
            var ret = index.GetEntities(atTile.TileIndex);
            ret.Remove(entity);
            return ret;
        }

        /// <summary>
        /// Get all entities with the same position as the given one.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static HashSet<Entity> GetColocatedEntitiesIncludingSelf(this Entity entity, Context<Entity> context)
        {
            if (!entity.HasComponent<ComponentAtTile>()) return new();
            var atTile = entity.GetComponent<ComponentAtTile>();
            var index = context.GetPositionIndex();
            var ret = index.GetEntities(atTile.TileIndex);
            return ret;
        }

        /// <summary>
        /// Get all components of the given type on entities with the same position as the given one.
        /// </summary>
        /// <typeparam name="ComponentType"></typeparam>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IEnumerable<ComponentType> GetColocatedComponentsIncludingSelf<ComponentType>(this Entity entity, Context<Entity> context) where ComponentType : IComponent, new()
        {
            return entity.GetColocatedEntitiesIncludingSelf(context)
                .Where(x => x.HasComponent<ComponentType>())
                .OrderBy(x => x.creationIndex)
                .Select(x => x.GetComponent<ComponentType>());
        }

        /// <summary>
        /// Get all components of the given type on other entities with the same position as the given one.
        /// </summary>
        /// <typeparam name="ComponentType"></typeparam>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IEnumerable<ComponentType> GetColocatedComponents<ComponentType>(this Entity entity, Context<Entity> context) where ComponentType : IComponent, new()
        {
            return entity.GetColocatedEntities(context)
                .Where(x => x.HasComponent<ComponentType>())
                .OrderBy(x => x.creationIndex)
                .Select(x => x.GetComponent<ComponentType>());
        }

        /// <summary>
        /// Get the first component of the given type on any entity at the same position as the given entity.
        /// </summary>
        /// <typeparam name="ComponentType"></typeparam>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static ComponentType GetAnyColocatedComponentIncludingSelf<ComponentType>(this Entity entity, Context<Entity> context) where ComponentType : IComponent, new()
        {
            if (entity.HasComponent<ComponentType>())
            {
                return entity.GetComponent<ComponentType>();
            }
            var ret = entity.GetColocatedEntities(context)
                .Where(x => x.HasComponent<ComponentType>())
                .OrderBy(x => x.creationIndex)
                .FirstOrDefault(null);
            return ret == null ? default : ret.GetComponent<ComponentType>();
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
    /// System to create the world.
    /// </summary>
    class SystemGenerate : MySystemBase, IInitializeSystem
    {
        public void Initialize()
        {
            var boardEntity = _context.GetEntities(Matcher<Entity>.AnyOf(ComponentBoard.TypeId)).First();
            var board = boardEntity.GetComponent<ComponentBoard>();
            int width = board.Width;
            int height = board.Height;
            for (int i = 0; i < width * height; ++i)
            {
                Entity tileEntity = _context.CreateTile(i);
                ComponentTile tile = tileEntity.GetComponent<ComponentTile>();
            }
        }
    }

    /// <summary>
    /// System to consume food.
    /// </summary>
    class SystemConsumeFood : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var entities = _context.GetEntities(Matcher<Entity>.AllOf(ComponentConsumesFood.TypeId, ComponentAtTile.TypeId));
            foreach (var e in entities)
            {
                var consumes = e.GetComponent<ComponentConsumesFood>();
                var tileEntity = e.GetTile(_context);
                var store = tileEntity.GetComponent<ComponentStoresFood>();
                int foodToConsume = consumes.FoodConsumedPerTick;
                store.FoodStored -= foodToConsume;
                foodToConsume = Math.Max(0, -store.FoodStored);
                store.FoodStored = Math.Max(0, store.FoodStored);
                consumes.DemandUnsatisfiedLastTick = foodToConsume;
            }
        }
    }

    /// <summary>
    /// System to produce food.
    /// </summary>
    class SystemProduceFood : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var entities = _context.GetEntities(Matcher<Entity>.AllOf(ComponentProducesFood.TypeId, ComponentAtTile.TypeId));
            foreach (var e in entities)
            {
                var tileEntity = e.GetTile(_context);
                var store = tileEntity.GetComponent<ComponentStoresFood>();
                var farm = e.GetComponent<ComponentProducesFood>();
                store.FoodStored += farm.FoodProducedPerTick;
            }
        }
    }

    /// <summary>
    /// System to manage population.
    /// </summary>
    class SystemPopulation : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var entities = _context.GetEntities(Matcher<Entity>.AllOf(ComponentPopulation.TypeId, ComponentAtTile.TypeId));
            foreach (var e in entities)
            {
                var population = e.GetComponent<ComponentPopulation>();
                if (population.NumberOfPeople > 0)
                {
                    var consumes = e.GetOrAddComponent<ComponentConsumesFood>();
                    population.NumberOfPeople = Math.Max(population.NumberOfPeople - consumes.DemandUnsatisfiedLastTick, 0);
                    consumes.FoodConsumedPerTick = population.NumberOfPeople;
                    if (population.NumberOfPeople == 0)
                    {
                        e.RemoveComponent(ComponentConsumesFood.TypeId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Try to ship food to where it is needed.
    /// </summary>
    class SystemMakeShipments : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var producerEntities = _context.GetEntities(Matcher<Entity>.AllOf(ComponentProducesFood.TypeId, ComponentAtTile.TypeId));
            var consumerEntities = _context.GetEntities(Matcher<Entity>.AllOf(ComponentConsumesFood.TypeId, ComponentAtTile.TypeId));
            foreach (var consumerEntity in consumerEntities)
            {
                var consumer = consumerEntity.GetComponent<ComponentConsumesFood>();
                var consumerLocation = consumerEntity.GetComponent<ComponentAtTile>();
                if (consumer.DemandUnsatisfiedLastTick > 0)
                {
                    var demand = consumer.FoodConsumedPerTick;
                    var orderedProducers = producerEntities.OrderByDescending(x => _context.DistanceBetween(x, consumerEntity));
                    foreach (var producerEntity in orderedProducers)
                    {
                        if (demand == 0) break;
                        var producerTileEntity = producerEntity.GetTile(_context);
                        var store = producerTileEntity.GetComponent<ComponentStoresFood>();
                        var producerLocation = producerEntity.GetComponent<ComponentAtTile>();
                        var shipped = demand;
                        store.FoodStored -= demand;
                        demand = Math.Max(0, -store.FoodStored);
                        shipped -= demand;
                        store.FoodStored = Math.Max(store.FoodStored, 0);
                        _context.CreateShipment(shipped, producerLocation.TileIndex, consumerLocation.TileIndex);
                    }
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
    class SystemPrepareShipments : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var toPrepare = _context.GetEntities(Matcher<Entity>
                .AllOf(ComponentShipment.TypeId, ComponentAtTile.TypeId)
                .NoneOf(ComponentPath.TypeId));
            foreach (var entity in toPrepare)
            {
                var position = entity.GetComponent<ComponentAtTile>();
                var shipment = entity.GetComponent<ComponentShipment>();
                var path = entity.GetOrAddComponent<ComponentPath>();
                path.TileIndices = GetPath(position.TileIndex, shipment.DestinationTileIndex);
                path.Current = 0;
            }
        }

        /// <summary>
        /// Compute a path with A*.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="finish"></param>
        /// <returns></returns>
        private int[] GetPath(int start, int finish)
        {
            var boardEntity = _context.GetBoardEntity();
            var board = boardEntity.GetComponent<ComponentBoard>();
            var astar = new AStar<int>(
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
    class SystemAdvanceShipments : MySystemBase, IExecuteSystem
    {
        public void Execute()
        {
            var toAdvance = _context.GetEntities(Matcher<Entity>.AllOf(
                ComponentShipment.TypeId, 
                ComponentAtTile.TypeId, 
                ComponentPath.TypeId,
                ComponentStoresFood.TypeId));
            foreach (var entity in toAdvance)
            {
                var position = entity.GetComponent<ComponentAtTile>();
                var path = entity.GetComponent<ComponentPath>();
                var shipment = entity.GetComponent<ComponentShipment>();

                // Check for lost shipments.
                if (path.TileIndices[path.Current] != position.TileIndex ||
                    path.TileIndices.Last() != shipment.DestinationTileIndex)
                {
                    entity.RemoveComponent(ComponentPath.TypeId);
                    break;
                }

                // Check for arrival
                if (position.TileIndex == shipment.DestinationTileIndex)
                {
                    var tileEntity = entity.GetTile(_context);
                    var tileInventory = tileEntity.GetComponent<ComponentStoresFood>();
                    var shipmentInventory = entity.GetComponent<ComponentStoresFood>();
                    tileInventory.FoodStored += shipmentInventory.FoodStored;
                    entity.Destroy();
                    break;
                }

                // Advance. Note that this depends on the path being
                // a correct representation of tile neighbours or the
                // shipment will simply teleport!
                position.TileIndex = path.TileIndices[path.Current++];
            }
        }
    }

    /// <summary>
    /// The program.
    /// </summary>
    class Program
    {
        public static void Main()
        {
            // Initialise
            var ctx = ComponentTypesRegistry.MakeContext();
            var board = ctx.CreateBoard(WorldSize.Typical);
            var systems = new MySystems(ctx);
            systems.Add(new SystemGenerate());
            systems.Add(new SystemPopulation());
            systems.Add(new SystemProduceFood());
            systems.Add(new SystemConsumeFood());
            systems.Add(new SystemMakeShipments());
            systems.Add(new SystemPrepareShipments());
            systems.Add(new SystemAdvanceShipments());

            // Update loop
            systems.Initialize();
            for (int i = 0; i < 100; ++i)
            {
                systems.Execute();
            }
            systems.Cleanup();
        }
    }
}