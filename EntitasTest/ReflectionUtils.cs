using Entitas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EntitasTest
{
    /// <summary>
    /// Static type ID cache. Useful when handling component types from a generic
    /// context, but if you know the concrete type you can just get the TypeId constant
    /// from it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class TypeIdOf<T> where T : IComponent
    {
        public static readonly int Id = ReflectionUtils.GetComponentTypeId(typeof(T));
    }

    static class ReflectionUtils
    {

        /// <summary>
        /// A component type is derived from Entitas.IComponent
        /// </summary>
        /// <param name="t">Type to test</param>
        /// <returns>True if t is derived from IComponent</returns>
        public static bool IsComponentType(System.Type t)
        {
            return typeof(IComponent).IsAssignableFrom(t) && t != typeof(IComponent);
        }

        /// <summary>
        /// Get a static ID to use for the component type. This relies on the type having
        /// a static int property called TypeId. An exception will be thrown if this criterion
        /// is not met.
        /// </summary>
        /// <param name="t">Type from which to obtain the id.</param>
        /// <returns>The value of the id</returns>
        public static int GetComponentTypeId(System.Type t)
        {
            PropertyInfo? info = t.GetProperty("TypeId", BindingFlags.Static);
            if (info == null)
            {
                throw new Exception("Component type should have TypeId property");
            }

            object? value = info.GetValue(t);
            if (value == null)
            {
                throw new Exception("Component type should have TypeId property & property should be set");
            }

            return (int)value;
        }

        /// <summary>
        /// A type associated with an ID.
        /// </summary>
        public struct TWithId
        {
            public System.Type T;
            public int Id;
        }

        /// <summary>
        /// Find all component types in the executing assembly.
        /// </summary>
        /// It is important that component ids are unique and that they form
        /// a contiguous block starting at 0; and exception will be thrown if
        /// this is not the case.
        /// <returns>An array of type, id pairs.</returns>
        public static TWithId[] FindComponentTypes()
        {
            var assemblies = new Assembly[] { Assembly.GetExecutingAssembly() };
            return FindComponentTypes(assemblies);
        }

        /// <summary>
        /// Find all component types.
        /// </summary>
        /// <param name="argAssembly">List of assemblies to query. </param>
        /// It is important that component ids are unique and that they form
        /// a contiguous block starting at 0; and exception will be thrown if
        /// this is not the case.
        /// <returns>An array of type, id pairs.</returns>
        public static TWithId[] FindComponentTypes(IEnumerable<Assembly> assemblies)
        {
            var types = assemblies.SelectMany(x => x.GetTypes()).Where(IsComponentType);
            return GetComponentTypeIds(types);
        }

        /// <summary>
        /// Get component type ids given a list of component types.
        /// </summary>
        /// <param name="typesIter">List of component types.</param>
        /// It is important that component ids are unique and that they form
        /// a contiguous block starting at 0; and exception will be thrown if
        /// this is not the case.
        /// <returns>An array of type, id pairs.</returns>
        public static TWithId[] GetComponentTypeIds(IEnumerable<Type> typesIter)
        {
            var types = typesIter
                .Select(t => new TWithId { T = t, Id = GetComponentTypeId(t) })
                .OrderBy(x => x.Id);
            int expected = 0;
            foreach (var x in types)
            {
                if (x.Id != expected++)
                {
                    throw new Exception("Unexpected gap in component ids. Entire range 0..N should be in use.");
                }
            }
            return types.ToArray();
        }
    }
}
