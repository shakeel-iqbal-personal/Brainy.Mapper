using System.Linq.Expressions;
using System.Reflection;

namespace Brainy.Mapper;

/// <summary>
/// Fired when a MapFrom resolver throws and the mapper is deliberately swallowing that
/// exception (returning a default value) instead of propagating it. Subscribe to this once,
/// e.g. at startup, to log these so a silently-defaulted value doesn't go completely unnoticed:
/// <code>
/// MapperDiagnostics.ResolverExceptionSwallowed += (memberName, ex) =>
///     logger.LogWarning(ex, "Mapper resolver for {Member} returned null after swallowing {Type}", memberName, ex.GetType().Name);
/// </code>
/// Swallowing NullReferenceException app-wide trades visibility for resilience - this event is
/// how you keep some of that visibility back without reintroducing the hard crash.
/// </summary>
public static class MapperDiagnostics
{
    public static event Action<string, Exception>? ResolverExceptionSwallowed;

    internal static void RaiseResolverExceptionSwallowed(string memberName, Exception exception)
        => ResolverExceptionSwallowed?.Invoke(memberName, exception);
}

internal class MappingExpression<TSource, TDestination> : IMappingExpression<TSource, TDestination>
{
    private readonly TypeMap _typeMap;

    public MappingExpression(TypeMap typeMap)
    {
        _typeMap = typeMap;
    }

    public IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions)
    {
        var memberName = GetMemberName(destinationMember);
        return ForMember(memberName, memberOptions);
    }

    public IMappingExpression<TSource, TDestination> ForMember<TMember>(string memberName, Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions)
    {
        var options = new MemberConfigurationExpression<TSource, TMember>();
        memberOptions(options);

        if (options.IsIgnored)
        {
            _typeMap.IgnoredMembers.Add(memberName);
        }
        else if (options.ResolverExpression != null)
        {
            var resolverExpression = options.ResolverExpression;
            _typeMap.MemberResolverExpressions[memberName] = resolverExpression;

            var compiled = resolverExpression.Compile();

            // NOTE: this lambda's body doesn't run here - it only runs later, once per row,
            // when Map() actually calls resolver(source). Wrapping *this* assignment in
            // try/catch can never catch a failure from inside the resolver, since nothing
            // inside it has executed yet. The try/catch has to live inside the lambda itself,
            // around the deferred DynamicInvoke call, so it wraps the moment the resolver
            // actually runs against a real row.
            _typeMap.MemberResolvers[memberName] = src =>
            {
                try
                {
                    return compiled.DynamicInvoke((TSource)src);
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    if (tie.InnerException is NullReferenceException)
                    {
                        // Swallowed by design (per your call) - but still raised so it can be
                        // logged if you subscribe to MapperDiagnostics.ResolverExceptionSwallowed.
                        // Worth treating this as a temporary safety net, not a permanent fix:
                        // each time this fires for a given member, that's a resolver that needs
                        // the same explicit null-guard treatment as CategoryName got.
                        MapperDiagnostics.RaiseResolverExceptionSwallowed(memberName, tie.InnerException);
                        return null;
                    }

                    // DynamicInvoke always wraps the resolver's real exception in a
                    // TargetInvocationException - unwrap it so the message/stack trace
                    // points at the actual failure (e.g. a null dereference inside the
                    // MapFrom(...) expression) instead of the invocation machinery.
                    throw new InvalidOperationException(
                        $"MapFrom resolver for member '{memberName}' threw {tie.InnerException.GetType().Name}: " +
                        $"{tie.InnerException.Message} - check for an unguarded null dereference " +
                        "(e.g. accessing a nested property that can be null) in this member's MapFrom(...) expression.",
                        tie.InnerException);
                }
            };
        }

        return this;
    }

    private static string GetMemberName<TMember>(Expression<Func<TDestination, TMember>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;

        // handles boxing conversions, e.g. value types wrapped in Convert()
        if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression inner)
            return inner.Member.Name;

        throw new ArgumentException("ForMember expression must be a simple property access, e.g. d => d.PropertyName");
    }
}

//internal class MemberConfigurationExpression<TSource, TMember> : IMemberConfigurationExpression<TSource, TMember>
//{
//    public Expression<Func<TSource, TMember>>? ResolverExpression { get; private set; }
//    public bool IsIgnored { get; private set; }

//    public void MapFrom(Expression<Func<TSource, TMember>> resolver) => ResolverExpression = resolver;

//    public void Ignore() => IsIgnored = true;

//    public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> resolver)
//    {
//        throw new NotImplementedException();
//    }
//}


internal class MemberConfigurationExpression<TSource, TDestinationMember>
: IMemberConfigurationExpression<TSource, TDestinationMember>
{
    public LambdaExpression? ResolverExpression { get; private set; }

    public bool IsIgnored { get; private set; }

    public void MapFrom(Expression<Func<TSource, TDestinationMember>> resolver) => ResolverExpression = resolver;

    public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> resolver)
    {
        ResolverExpression = resolver;
    }

    public void Ignore()
    {
        IsIgnored = true;
    }
}
