namespace Brainy.Mapper;

/// <summary>
/// Mutable builder passed to the configuration action, analogous to AutoMapper's IMapperConfigurationExpression.
/// </summary>
public class MapperConfigurationExpression
{
    internal readonly Dictionary<(Type Source, Type Dest), TypeMap> TypeMaps = new();
    internal readonly List<Profile> Profiles = new();

    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var key = (typeof(TSource), typeof(TDestination));
        if (!TypeMaps.TryGetValue(key, out var typeMap))
        {
            typeMap = new TypeMap(typeof(TSource), typeof(TDestination));
            TypeMaps[key] = typeMap;
        }
        return new MappingExpression<TSource, TDestination>(typeMap);
    }

    public void AddProfile(Profile profile) => Profiles.Add(profile);

    public void AddProfile<TProfile>() where TProfile : Profile, new() => Profiles.Add(new TProfile());
}

/// <summary>
/// Holds the compiled/validated mapping configuration and produces IMapper instances.
/// Build once (typically at app startup / via DI) and reuse - it's thread-safe after construction.
/// </summary>
public class MapperConfiguration
{
    internal readonly Dictionary<(Type Source, Type Dest), TypeMap> TypeMaps;

    public MapperConfiguration(Action<MapperConfigurationExpression> configure)
    {
        var expression = new MapperConfigurationExpression();
        configure(expression);

        // profiles may themselves call CreateMap; merge their deferred registrations in
        foreach (var profile in expression.Profiles)
        {
            foreach (var action in profile.Actions)
                action(expression);
        }

        TypeMaps = expression.TypeMaps;
    }

    public IMapper CreateMapper() => new Mapper(this);
}
