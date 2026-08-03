namespace Brainy.Mapper;

internal static class CollectionSupport
{
    /// <summary>
    /// True if the type is an enumerable collection (excluding string), with itemType set
    /// to the element type. Used to decide when to map element-by-element vs. directly.
    /// </summary>
    public static bool IsCollection(Type type, out Type? itemType)
    {
        itemType = null;
        if (type == typeof(string)) return false;

        if (type.IsArray)
        {
            itemType = type.GetElementType();
            return true;
        }

        var enumerableInterface = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface != null)
        {
            itemType = enumerableInterface.GetGenericArguments()[0];
            return true;
        }

        return false;
    }
}
