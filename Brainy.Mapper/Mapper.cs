using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Brainy.Mapper;

/// <summary>
/// Reflection-based object mapper. Thread-safe; reuse a single instance built from a
/// MapperConfiguration (typically registered as a singleton in DI).
/// </summary>
public class Mapper : IMapper, IRuntimeMapper
{
    private readonly MapperConfiguration _config;

    /// <summary>The configuration this mapper was built from. Used by the ProjectTo extension methods.</summary>
    public MapperConfiguration Configuration => _config;

    // Cache of destination PropertyInfo[] per type to avoid repeated reflection lookups.
    // ConcurrentDictionary because Mapper instances are meant to be shared/reused across threads.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> WritablePropsCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadablePropsCache = new();

    public Mapper(MapperConfiguration config)
    {
        _config = config;
    }

    public TDestination Map<TDestination>(object source)
    {
        if (source is null) return default!;
        return (TDestination)Map(source, source.GetType(), typeof(TDestination))!;
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null) return default!;
        return (TDestination)Map(source!, typeof(TSource), typeof(TDestination))!;
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        // Map-onto semantics: a null source leaves the existing destination untouched
        // rather than wiping it out, matching AutoMapper's Map(source, destination) behavior.
        if (source is null) return destination;

        // This overload calls MapOnto directly rather than going through Map(object,Type,Type),
        // so it doesn't get that method's collection handling for free - guard against it
        // explicitly here instead of silently trying to map List<T>'s own properties.
        if (IsCollection(typeof(TSource), out _) && IsCollection(typeof(TDestination), out _))
            throw new NotSupportedException(
                "Map(source, destination) doesn't support mapping directly onto an existing " +
                "top-level collection - it's ambiguous whether that should replace or append to " +
                "the existing items. Use Map<TSource,TDestination>(source) instead, which returns " +
                "a new collection.");

        MapOnto(source!, destination!, typeof(TSource), typeof(TDestination));
        return destination;
    }

    public object? Map(object source, Type sourceType, Type destinationType)
    {
        if (source is null) return null;

        // Without this check, mapping e.g. List<Permission> -> List<FlatPermissionModel>
        // directly (not as a nested property) would fall through to the object-mapping path
        // below: Activator.CreateInstance would build an empty destination list, then MapOnto
        // would try to match *List<T>'s own properties* (Capacity, etc.) between the two list
        // types - never touching the items inside - silently returning an empty list instead
        // of throwing, which is a much nastier bug to track down than a clear error would be.
        if (IsCollection(sourceType, out var sourceItemType) && IsCollection(destinationType, out var destItemType))
            return MapCollection((IEnumerable)source, sourceItemType!, destItemType!, destinationType);

        var destination = Activator.CreateInstance(destinationType)
            ?? throw new InvalidOperationException(
                $"Could not create an instance of '{destinationType.Name}'. It needs a public parameterless constructor.");

        MapOnto(source, destination, sourceType, destinationType);
        return destination;
    }

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source)
    {
        return QueryableExtensions.ProjectToCore<TDestination>(source, _config);
    }

    // Explicit interface implementation used internally for nested/collection mapping,
    // where a null source or missing config should resolve to null/skip rather than throw eagerly.
    object? IRuntimeMapper.MapValue(object? source, Type sourceType, Type destinationType)
    {
        if (source is null) return null;

        if (destinationType.IsAssignableFrom(sourceType))
            return source;

        if (IsCollection(sourceType, out var sourceItemType) && IsCollection(destinationType, out var destItemType))
            return MapCollection((IEnumerable)source, sourceItemType!, destItemType!, destinationType);

        return Map(source, sourceType, destinationType);
    }

    private void MapOnto(object source, object destination, Type sourceType, Type destinationType)
    {
        var typeMap = GetTypeMap(sourceType, destinationType);
        var destProps = GetWritableProperties(destinationType);
        var sourceProps = GetReadableProperties(sourceType);

        foreach (var destProp in destProps)
        {
            if (typeMap != null && typeMap.IgnoredMembers.Contains(destProp.Name))
                continue;

            // 1. custom resolver from ForMember(...).MapFrom(...)
            if (typeMap != null && typeMap.MemberResolvers.TryGetValue(destProp.Name, out var resolver))
            {
                var value = resolver(source);
                AssignValue(destination, destProp, value, value?.GetType() ?? destProp.PropertyType);
                continue;
            }

            // 2. direct convention match: same property name
            var sourceProp = Array.Find(sourceProps, p => p.Name == destProp.Name);

            // 3. flattening convention: e.g. Destination.AddressCity <- Source.Address.City
            if (sourceProp is null)
            {
                var flattened = TryResolveFlattenedPath(source, sourceType, destProp.Name);
                if (flattened.Found)
                {
                    AssignValue(destination, destProp, flattened.Value, flattened.ValueType!);
                    continue;
                }
            }

            if (sourceProp is null || !sourceProp.CanRead)
                continue;

            var rawValue = sourceProp.GetValue(source);
            AssignValue(destination, destProp, rawValue, sourceProp.PropertyType);
        }
    }

    private void AssignValue(object destination, PropertyInfo destProp, object? rawValue, Type valueRuntimeType)
    {
        if (rawValue is null)
        {
            if (!destProp.PropertyType.IsValueType || Nullable.GetUnderlyingType(destProp.PropertyType) != null)
                destProp.SetValue(destination, null);
            return;
        }

        var destType = destProp.PropertyType;

        if (destType.IsAssignableFrom(valueRuntimeType))
        {
            destProp.SetValue(destination, rawValue);
            return;
        }

        if (IsCollection(valueRuntimeType, out var srcItemType) && IsCollection(destType, out var destItemType))
        {
            var mappedCollection = MapCollection((IEnumerable)rawValue, srcItemType!, destItemType!, destType);
            destProp.SetValue(destination, mappedCollection);
            return;
        }

        // Scalar conversions (enum <-> int, int <-> long, etc.) - do this before falling to
        // nested-object mapping, otherwise e.g. an enum property mapped to an int DTO property
        // would silently end up 0: Activator.CreateInstance(typeof(int)) succeeds and produces
        // a default int, then there are no properties on int to copy anything into.
        if (TryConvertScalar(rawValue, valueRuntimeType, destType, out var converted))
        {
            destProp.SetValue(destination, converted);
            return;
        }

        // nested complex object - recurse (requires a CreateMap for the nested types,
        // unless the types happen to line up, same as AutoMapper's behavior)
        var nested = Map(rawValue, valueRuntimeType, destType);
        destProp.SetValue(destination, nested);
    }

    // True for enums, numeric primitives, and decimal (including their Nullable<T> forms) -
    // types where a value can be directly converted rather than member-by-member mapped.
    private static bool IsScalarConvertible(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        return underlying.IsEnum || underlying.IsPrimitive || underlying == typeof(decimal);
    }

    private static bool TryConvertScalar(object rawValue, Type sourceType, Type destType, out object? converted)
    {
        converted = null;

        var sourceUnderlying = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var destUnderlying = Nullable.GetUnderlyingType(destType) ?? destType;

        // Nullable<T> <-> T for the SAME T (e.g. Guid? -> Guid). rawValue is already known
        // non-null here (checked earlier in AssignValue), and thanks to how the CLR boxes a
        // non-null Nullable<T> (it boxes as plain T, not as a boxed Nullable<T>), rawValue is
        // already effectively the right runtime object either way - no conversion needed.
        if (sourceUnderlying == destUnderlying && sourceUnderlying.IsValueType && sourceType != destType)
        {
            converted = rawValue;
            return true;
        }

        // enum -> string: just the member name (rawValue is already known non-null here).
        if (sourceUnderlying.IsEnum && destType == typeof(string))
        {
            converted = rawValue.ToString();
            return true;
        }

        // string -> enum: unlike ProjectTo (which can't throw mid-query-translation), Map()
        // runs in memory, so an unmatched string is a real bug worth surfacing immediately
        // rather than silently defaulting.
        if (sourceType == typeof(string) && destUnderlying.IsEnum)
        {
            var text = (string)rawValue;
            if (!Enum.TryParse(destUnderlying, text, ignoreCase: true, out var parsed))
                throw new InvalidOperationException(
                    $"Could not convert '{text}' to enum type '{destUnderlying.Name}' - no matching member found (case-insensitive).");

            converted = parsed;
            return true;
        }

        if (!IsScalarConvertible(sourceType) || !IsScalarConvertible(destType))
            return false;

        try
        {
            converted = destUnderlying.IsEnum
                ? Enum.ToObject(destUnderlying, rawValue)
                : Convert.ChangeType(rawValue, destUnderlying);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            // e.g. two unrelated enums, or a numeric value that doesn't fit the target type
            return false;
        }
    }

    private object MapCollection(IEnumerable source, Type sourceItemType, Type destItemType, Type destCollectionType)
    {
        var listType = typeof(List<>).MakeGenericType(destItemType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in source)
        {
            if (item is null)
            {
                list.Add(null);
                continue;
            }

            if (destItemType.IsAssignableFrom(item.GetType()))
            {
                list.Add(item);
            }
            else
            {
                list.Add(Map(item, sourceItemType, destItemType));
            }
        }

        if (destCollectionType.IsArray)
        {
            var array = Array.CreateInstance(destItemType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        if (destCollectionType.IsInterface || destCollectionType.IsAssignableFrom(listType))
            return list;

        // destination is a concrete collection type (e.g. a custom List subclass) - try to construct it directly
        var concrete = Activator.CreateInstance(destCollectionType) as IList;
        if (concrete != null)
        {
            foreach (var item in list) concrete.Add(item);
            return concrete;
        }

        return list;
    }

    private (bool Found, object? Value, Type? ValueType) TryResolveFlattenedPath(object source, Type sourceType, string destMemberName)
    {
        // Try splitting the destination member name at each PascalCase word boundary and
        // walking that as a chain of properties, e.g. "AddressCity" -> source.Address.City
        for (int i = 1; i < destMemberName.Length; i++)
        {
            if (!char.IsUpper(destMemberName[i])) continue;

            var head = destMemberName[..i];
            var tail = destMemberName[i..];

            var headProp = Array.Find(GetReadableProperties(sourceType), p => p.Name == head);
            if (headProp is null) continue;

            var headValue = headProp.GetValue(source);
            if (headValue is null) return (true, null, headProp.PropertyType);

            var tailProps = GetReadableProperties(headValue.GetType());
            var tailProp = Array.Find(tailProps, p => p.Name == tail);
            if (tailProp != null)
                return (true, tailProp.GetValue(headValue), tailProp.PropertyType);
        }

        return (false, null, null);
    }

    private TypeMap? GetTypeMap(Type sourceType, Type destinationType)
    {
        _config.TypeMaps.TryGetValue((sourceType, destinationType), out var map);
        return map;
    }

    private static bool IsCollection(Type type, out Type? itemType) => CollectionSupport.IsCollection(type, out itemType);

    private static PropertyInfo[] GetWritableProperties(Type type) =>
        WritablePropsCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToArray());

    private static PropertyInfo[] GetReadableProperties(Type type) =>
        ReadablePropsCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray());
}
