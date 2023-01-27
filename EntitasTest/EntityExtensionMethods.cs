using Entitas;

namespace EntitasTest
{

    static class EntityExtensionMethods
    {
        public static ComponentType AddComponent<ComponentType>(this Entitas.Entity entity) where ComponentType : IComponent, new()
        {
            int id = TypeIdOf<ComponentType>.Id;
            var component = entity.CreateComponent<ComponentType>(id);
            entity.AddComponent(id, component);
            return component;
        }

        public static ComponentType GetOrAddComponent<ComponentType>(this Entitas.Entity entity) where ComponentType : IComponent, new()
        {
            int id = TypeIdOf<ComponentType>.Id;
            if (entity.HasComponent(id))
            {
                return (ComponentType)entity.GetComponent(id);
            } else
            {
                return entity.AddComponent<ComponentType>();
            }
        }

        public static ComponentType GetComponent<ComponentType>(this Entitas.Entity entity) where ComponentType : IComponent, new()
        {
            return (ComponentType)entity.GetComponent(TypeIdOf<ComponentType>.Id);
        }

        public static bool HasComponent<ComponentType>(this Entitas.Entity entity) where ComponentType : IComponent, new()
        {
            return entity.HasComponent(TypeIdOf<ComponentType>.Id);
        }

        class Program
    {
        /**
         * Questions
         * 
         *    - is 'normal' code reasonably performant?
         *    - is serialization painless without additional work?
         *    - can updates be done on a separate thread?
         *
         * Assuming a 1024x1024 board:
         *   - 1,048,576 tile entities
         *   - each tile has
         *     - 0-1 rivers
         *     - 0-1 cities
         *     - 0-n works
         *     - 0-n demands
         *     - ...
         *
         */
        static void Main(string[] args)
        {
            var ctx = ComponentTypesRegistry.MakeContext();
            var entity1 = ctx.CreateEntity();
            entity1.AddComponent(ComponentOne.TypeId, new ComponentOne());
            entity1.AddComponent(ComponentTwo.TypeId, new ComponentTwo());
            var entity2 = ctx.CreateEntity();
            entity2.AddComponent(ComponentTwo.TypeId, new ComponentTwo());
            entity2.AddComponent(ComponentThree.TypeId, new ComponentThree());
        }
    }
}
