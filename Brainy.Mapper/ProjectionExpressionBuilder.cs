using System.Linq.Expressions;
using System.Reflection;

namespace Brainy.Mapper;


/// <summary>
/// Builds a LambdaExpression equivalent to "src => new TDestination { ... }" directly from a
/// MapperConfiguration, using only expression-tree-safe constructs (property access, MemberInit,
/// Enumerable.Select/ToList/ToArray, conditional null checks) so LINQ providers such as EF Core
/// can translate the whole thing into a single SQL query instead of pulling entities into memory.
/// </summary>
internal static class ProjectionExpressionBuilder
{
    public static LambdaExpression BuildSelector(MapperConfiguration config, Type sourceType, Type destType)
    {
        var param = Expression.Parameter(sourceType, "src");
        // isRoot: true - the element passed into Select(src => ...) is never null for a
        // materialized entity query, so we skip the null-guard ternary at this one level.
        var body = BuildValueExpression(destType, param, sourceType, config, new HashSet<(Type, Type)>(), isRoot: true);
        return Expression.Lambda(body, param);
    }

    private static Expression BuildValueExpression(
        Type destType, Expression sourceExpr, Type sourceType, MapperConfiguration config,
        HashSet<(Type, Type)> visiting, bool isRoot = false)
    {
        if (destType.IsAssignableFrom(sourceType))
            return sourceExpr;

        // Scalar conversions (enum <-> int, int <-> long, etc.) aren't an object graph to
        // construct - they're a single Convert(...) node. Handle these before falling through
        // to object/collection mapping, otherwise e.g. an enum property mapped to an int DTO
        // property incorrectly gets treated as "build me a new int {...}".
        if (TryBuildScalarConversion(sourceExpr, sourceType, destType, out var converted))
            return converted!;

        if (CollectionSupport.IsCollection(sourceType, out var sourceItemType) &&
            CollectionSupport.IsCollection(destType, out var destItemType))
        {
            return BuildCollectionExpression(destType, sourceExpr, sourceType, sourceItemType!, destItemType!, config, visiting, isRoot);
        }

        return BuildObjectExpression(destType, sourceExpr, sourceType, config, visiting, isRoot);
    }

    // True for enums, numeric primitives, and decimal (including their Nullable<T> forms) -
    // types where a value can be directly Convert()-ed rather than member-by-member mapped.
    private static bool IsScalarConvertible(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        return underlying.IsEnum || underlying.IsPrimitive || underlying == typeof(decimal);
    }

    private static bool TryBuildScalarConversion(Expression sourceExpr, Type sourceType, Type destType, out Expression? result)
    {
        result = null;

        var sourceUnderlying = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var destUnderlying = Nullable.GetUnderlyingType(destType) ?? destType;

        // Nullable<T> <-> T for the SAME T (e.g. Guid? -> Guid, DateTime -> DateTime?,
        // TimeSpan? -> TimeSpan). This is a universal CLR nullable-lifting conversion that
        // works for any value type, not just numerics/enums - EF Core translates it as-is
        // (a nullable column read directly into a non-nullable one, or vice versa).
        // Note: converting a null Guid?/DateTime?/etc. into its non-nullable form will still
        // throw at execution time, same as it would with `.Value` in plain C# - if the source
        // can genuinely be null, either keep the destination nullable too, or use
        // ForMember(...).MapFrom(s => s.Field ?? someDefault) to supply a fallback.
        if (sourceUnderlying == destUnderlying && sourceUnderlying.IsValueType && sourceType != destType)
        {
            result = Expression.Convert(sourceExpr, destType);
            return true;
        }

        // enum -> string: there's no CLR conversion operator for this, so Expression.Convert
        // can't do it, and calling .ToString() isn't SQL-translatable either (that forces EF
        // Core to fall back to client evaluation). Instead build "src == V1 ? "V1" : src == V2
        // ? "V2" : ... : null" - a chain of equality checks EF Core translates into a SQL CASE.
        if (sourceUnderlying.IsEnum && destType == typeof(string))
        {
            result = BuildEnumToStringExpression(sourceExpr, sourceType, sourceUnderlying);
            return true;
        }

        // string -> enum: same idea in reverse. Unmatched or null strings fall through to
        // default(TEnum) (or null for a nullable enum destination) rather than throwing, since
        // a query expression can't throw mid-translation - Map() (see Mapper.cs) can afford to
        // be stricter here because it runs in memory.
        if (sourceType == typeof(string) && destUnderlying.IsEnum)
        {
            result = BuildStringToEnumExpression(sourceExpr, destType, destUnderlying);
            return true;
        }

        if (!IsScalarConvertible(sourceType) || !IsScalarConvertible(destType))
            return false;

        try
        {
            // Expression.Convert covers enum<->underlying-numeric-type and standard numeric
            // widening/narrowing conversions - EF Core translates these to a plain SQL CAST.
            result = Expression.Convert(sourceExpr, destType);
            return true;
        }
        catch (InvalidOperationException)
        {
            // No built-in conversion between these two scalar types (e.g. two unrelated enums).
            return false;
        }
    }

    // Builds: src == V1 ? "V1" : src == V2 ? "V2" : ... : null
    // Works for both TEnum and TEnum? sourceExpr - comparing a null Nullable<TEnum> against each
    // non-null constant naturally evaluates false for every branch, so it falls through to the
    // null fallback without needing a separate null check.
    private static Expression BuildEnumToStringExpression(Expression sourceExpr, Type sourceType, Type enumType)
    {
        var names = Enum.GetNames(enumType);
        var values = Enum.GetValues(enumType);

        Expression chain = Expression.Constant(null, typeof(string));

        for (int i = 0; i < names.Length; i++)
        {
            var value = values.GetValue(i)!;
            Expression valueConst = Expression.Constant(value, enumType);
            if (sourceType != enumType)
                valueConst = Expression.Convert(valueConst, sourceType); // wrap into Nullable<TEnum>

            var equals = Expression.Equal(sourceExpr, valueConst);
            chain = Expression.Condition(equals, Expression.Constant(names[i], typeof(string)), chain);
        }

        return chain;
    }

    // Builds: src == "V1" ? V1 : src == "V2" ? V2 : ... : default(TDest)
    // default(TDest) is null when destType is a nullable enum, or the enum's 0 value otherwise -
    // used for both an unmatched string and a null string.
    private static Expression BuildStringToEnumExpression(Expression sourceExpr, Type destType, Type enumType)
    {
        var names = Enum.GetNames(enumType);
        var values = Enum.GetValues(enumType);

        Expression chain = Expression.Default(destType);

        for (int i = 0; i < names.Length; i++)
        {
            var value = values.GetValue(i)!;
            Expression valueConst = Expression.Constant(value, enumType);
            if (destType != enumType)
                valueConst = Expression.Convert(valueConst, destType); // wrap into Nullable<TEnum>

            var equals = Expression.Equal(sourceExpr, Expression.Constant(names[i], typeof(string)));
            chain = Expression.Condition(equals, valueConst, chain);
        }

        return chain;
    }

    private static Expression BuildObjectExpression(
        Type destType, Expression sourceExpr, Type sourceType, MapperConfiguration config,
        HashSet<(Type, Type)> visiting, bool isRoot = false)
    {
        var pairKey = (sourceType, destType);
        if (!visiting.Add(pairKey))
            throw new InvalidOperationException(
                $"Circular mapping detected between '{sourceType.Name}' and '{destType.Name}'. " +
                "ProjectTo can't build an expression tree for self-referencing/circular object graphs " +
                "- use Map() for those instead, or ForMember(...).Ignore() to break the cycle.");

        config.TypeMaps.TryGetValue((sourceType, destType), out var typeMap);

        if (IsScalarConvertible(destType) || IsScalarConvertible(sourceType) || destType == typeof(string) || sourceType == typeof(string))
            throw new InvalidOperationException(
                $"ProjectTo doesn't know how to convert '{sourceType.Name}' to '{destType.Name}'. " +
                "These look like scalar values rather than an object to map member-by-member " +
                "(e.g. two unrelated enums, or a string that needs parsing). " +
                "If this needs custom logic, use ForMember(...).MapFrom(src => ...) instead - " +
                "keep in mind the expression still needs to be SQL-translatable for ProjectTo to work.");

        if (destType.GetConstructor(Type.EmptyTypes) == null)
            throw new InvalidOperationException(
                $"ProjectTo requires destination type '{destType.Name}' to have a public parameterless constructor.");

        var destProps = destType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

        var sourceProps = sourceType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        var bindings = new List<MemberBinding>();

        foreach (var destProp in destProps)
        {
            if (typeMap != null && typeMap.IgnoredMembers.Contains(destProp.Name))
                continue;

            Expression? valueExpr = null;

            if (typeMap != null && typeMap.MemberResolverExpressions.TryGetValue(destProp.Name, out var resolverLambda))
            {
                // Inline the resolver's body into our expression tree, replacing its parameter
                // with the current source expression (e.g. "src" or "src.Address").
                valueExpr = new ParameterReplacer(resolverLambda.Parameters[0], sourceExpr).Visit(resolverLambda.Body);
            }
            else
            {
                var sourceProp = Array.Find(sourceProps, p => p.Name == destProp.Name);
                if (sourceProp != null)
                {
                    var propExpr = Expression.Property(sourceExpr, sourceProp);
                    valueExpr = BuildValueExpression(destProp.PropertyType, propExpr, sourceProp.PropertyType, config, visiting);
                }
                else if (TryBuildFlattenedExpression(sourceExpr, sourceType, destProp.Name, out var flattened))
                {
                    valueExpr = BuildValueExpression(destProp.PropertyType, flattened!, flattened!.Type, config, visiting);
                }
            }

            if (valueExpr == null)
                continue; // no source, no resolver - leave the destination member at its default

            if (valueExpr.Type != destProp.PropertyType)
            {
                // Most commonly this happens when a custom MapFrom(...) resolver returns a
                // source-side type directly (e.g. .MapFrom(s => s.TaskType), returning the
                // entity, not the mapped DTO). Route it back through the full builder so it
                // gets properly object-mapped/converted, instead of assuming a raw CLR
                // coercion exists - Expression.Convert only handles numeric widening and
                // reference up/downcasts, and throws "No coercion operator is defined..."
                // for anything that actually needs member-by-member mapping.
                valueExpr = BuildValueExpression(destProp.PropertyType, valueExpr, valueExpr.Type, config, visiting);
            }

            bindings.Add(Expression.Bind(destProp, valueExpr));
        }

        visiting.Remove(pairKey);

        Expression memberInit = Expression.MemberInit(Expression.New(destType), bindings);

        // Only guard nested navigation properties (they can genuinely be null). The root
        // element of Select(...) can't be, and wrapping it in a redundant "src == null ? null
        // : new T{...}" ternary is what breaks EF Core's ability to compose the query further -
        // e.g. Union() right after ProjectTo() - forcing it into client evaluation instead.
        return isRoot ? memberInit : WrapWithNullCheckIfNeeded(sourceExpr, sourceType, destType, memberInit);
    }

    private static Expression BuildCollectionExpression(
        Type destCollectionType, Expression sourceExpr, Type sourceType,
        Type sourceItemType, Type destItemType, MapperConfiguration config,
        HashSet<(Type, Type)> visiting, bool isRoot = false)
    {
        var itemParam = Expression.Parameter(sourceItemType, "x");
        // Same reasoning as the root parameter: each element streamed through Enumerable.Select
        // is a real materialized item, never null itself, so skip the guard for it too - only
        // this item's own nested properties get null-checked where relevant.
        var itemBody = BuildValueExpression(destItemType, itemParam, sourceItemType, config, visiting, isRoot: true);
        var itemLambda = Expression.Lambda(itemBody, itemParam);

        var selectCall = Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Select), new[] { sourceItemType, destItemType }, sourceExpr, itemLambda);

        Expression result = destCollectionType.IsArray
            ? Expression.Call(typeof(Enumerable), nameof(Enumerable.ToArray), new[] { destItemType }, selectCall)
            : Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { destItemType }, selectCall);

        if (result.Type != destCollectionType)
            result = Expression.Convert(result, destCollectionType);

        return isRoot ? result : WrapWithNullCheckIfNeeded(sourceExpr, sourceType, destCollectionType, result);
    }

    // If the source side can be null (a reference type), guard the projected value with a
    // ternary so a null navigation property doesn't blow up as a NullReferenceException.
    // Skipped when the destination is a non-nullable value type, since null wouldn't be valid there.
    private static Expression WrapWithNullCheckIfNeeded(Expression sourceExpr, Type sourceType, Type destType, Expression valueExpr)
    {
        if (sourceType.IsValueType)
            return valueExpr;

        var destAcceptsNull = !destType.IsValueType || Nullable.GetUnderlyingType(destType) != null;
        if (!destAcceptsNull)
            return valueExpr;

        var nullCheck = Expression.Equal(sourceExpr, Expression.Constant(null, sourceType));
        return Expression.Condition(nullCheck, Expression.Constant(null, destType), valueExpr);
    }

    // Mirrors AutoMapper's flattening convention: DestMemberName "AddressCity" resolves to
    // source.Address.City when there's no direct "AddressCity" property.
    private static bool TryBuildFlattenedExpression(Expression sourceExpr, Type sourceType, string destMemberName, out Expression? result)
    {
        result = null;
        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToArray();

        for (int i = 1; i < destMemberName.Length; i++)
        {
            if (!char.IsUpper(destMemberName[i])) continue;

            var head = destMemberName[..i];
            var tail = destMemberName[i..];

            var headProp = Array.Find(sourceProps, p => p.Name == head);
            if (headProp == null) continue;

            var tailProp = headProp.PropertyType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.CanRead && p.Name == tail);
            if (tailProp == null) continue;

            var headExpr = Expression.Property(sourceExpr, headProp);
            var tailAccess = (Expression)Expression.Property(headExpr, tailProp);

            // headProp (e.g. "Address") can be null even when the whole chain resolves - guard it
            // the same way BuildObjectExpression guards direct nested-object mapping, so a null
            // navigation property yields a default value here instead of throwing.
            if (!headProp.PropertyType.IsValueType)
            {
                var nullCheck = Expression.Equal(headExpr, Expression.Constant(null, headProp.PropertyType));
                var isNullableTail = !tailProp.PropertyType.IsValueType || Nullable.GetUnderlyingType(tailProp.PropertyType) != null;
                var defaultValue = isNullableTail
                    ? Expression.Constant(null, tailProp.PropertyType)
                    : (Expression)Expression.Default(tailProp.PropertyType);

                tailAccess = Expression.Condition(nullCheck, defaultValue, tailAccess);
            }

            result = tailAccess;
            return true;
        }

        return false;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        public ParameterReplacer(ParameterExpression from, Expression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
