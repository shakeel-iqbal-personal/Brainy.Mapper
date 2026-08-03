using System.Linq.Expressions;

namespace Brainy.Mapper;

/// <summary>
/// ProjectTo builds a Select(src => new TDestination {...}) expression from your mapping
/// configuration and applies it directly to the IQueryable, so EF Core (or any LINQ provider)
/// translates it into SQL that only selects the columns you actually need. This is the
/// projection equivalent of Map() - use it for read queries against a DbSet/IQueryable,
/// and use Map()/Map&lt;T&gt;() once you already have materialized objects in memory.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>Projects a queryable onto TDestination using a mapper configuration directly.</summary>
    public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, MapperConfiguration configuration)
        => ProjectToCore<TDestination>(source, configuration);

    /// <summary>Convenience overload: dbContext.Users.ProjectTo&lt;UserDto&gt;(mapper).</summary>
    public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IMapper mapper)
    {
        if (mapper is not Mapper concrete)
            throw new NotSupportedException("ProjectTo requires an IMapper created via MapperConfiguration.CreateMapper().");

        return ProjectToCore<TDestination>(source, concrete.Configuration);
    }

    internal static IQueryable<TDestination> ProjectToCore<TDestination>(IQueryable source, MapperConfiguration configuration)
    {
        var sourceType = source.ElementType;
        var destType = typeof(TDestination);

        var lambda = ProjectionExpressionBuilder.BuildSelector(configuration, sourceType, destType);

        var selectCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            new[] { sourceType, destType },
            source.Expression,
            lambda);

        return (IQueryable<TDestination>)source.Provider.CreateQuery<TDestination>(selectCall);
    }
}
