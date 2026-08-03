using System.Linq.Expressions;

namespace Brainy.Mapper;

/// <summary>
/// Represents the mapping configuration between one source type and one destination type.
/// </summary>
internal class TypeMap
{
    public Type SourceType { get; }
    public Type DestinationType { get; }

    // member name -> compiled custom resolver, used by the in-memory Mapper (takes source object, returns value to assign)
    internal Dictionary<string, Func<object, object?>> MemberResolvers { get; } = new();

    // member name -> the same resolver as a raw expression tree (Expression<Func<TSource,TMember>>),
    // used by ProjectTo so the resolver logic can be inlined into a translatable LINQ query.
    internal Dictionary<string, LambdaExpression> MemberResolverExpressions { get; } = new();

    // member names to skip entirely (Ignore())
    internal HashSet<string> IgnoredMembers { get; } = new();

    // cached compiled mapper delegate, built lazily on first use
    internal Func<object, object?, IRuntimeMapper, object>? CompiledMapper { get; set; }

    public TypeMap(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }
}

/// <summary>
/// Internal contract used by generated mapping functions to recursively resolve
/// nested/collection mappings without depending on the public Mapper class directly.
/// </summary>
internal interface IRuntimeMapper
{
    object? MapValue(object? source, Type sourceType, Type destinationType);
}
