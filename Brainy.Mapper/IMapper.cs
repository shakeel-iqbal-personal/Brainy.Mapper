namespace Brainy.Mapper;

/// <summary>
/// Main entry point for performing mappings, analogous to AutoMapper's IMapper.
/// </summary>
public interface IMapper
{
    TDestination Map<TDestination>(object source);
    TDestination Map<TSource, TDestination>(TSource source);
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    object? Map(object source, Type sourceType, Type destinationType);

    /// <summary>
    /// Projects an IQueryable (e.g. an EF Core DbSet or query) onto TDestination by building
    /// a Select(...) expression from the configured mapping, so the provider translates it into
    /// SQL that only pulls the columns you need - unlike Map(), which requires materializing
    /// full source entities first. Equivalent to AutoMapper's queryable.ProjectTo&lt;TDestination&gt;().
    /// </summary>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source);
}
