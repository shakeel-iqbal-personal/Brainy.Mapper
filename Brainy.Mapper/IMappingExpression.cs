using System.Linq.Expressions;

namespace Brainy.Mapper;

public interface IMappingExpression<TSource, TDestination>
{
    /// <summary>
    /// Customize how a specific destination member is populated.
    /// Example: cfg.ForMember(d => d.FullName, opt => opt.MapFrom(s => s.First + " " + s.Last));
    /// </summary>
    IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions);

    IMappingExpression<TSource, TDestination> ForMember<TMember>(
       string destinationMember,
       Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions);
}

public interface IMemberConfigurationExpression<TSource, TMember>
{
    ///// <summary>
    ///// Populate this destination member from a custom source expression, e.g.
    ///// opt.MapFrom(s => s.FirstName + " " + s.LastName).
    ///// This is an expression tree (not a plain delegate) so it can be both compiled for
    ///// in-memory Map() calls and inlined into ProjectTo() queries for SQL translation.
    ///// Because of that, the body must be valid as an expression (no statements, loops, or
    ///// side-effecting code) - the same restriction LINQ providers like EF Core impose.
    ///// </summary>
    //void MapFrom(Expression<Func<TSource, TMember>> resolver);

    void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> resolver);

    /// <summary>Skip this destination member entirely - it will keep its default value.</summary>
    void Ignore();
}
