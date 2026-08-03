namespace Brainy.Mapper;

/// <summary>
/// Base class for grouping related CreateMap calls, mirroring AutoMapper's Profile.
/// Override Configure() and call CreateMap there, or call CreateMap from the constructor.
/// </summary>
public abstract class Profile
{
    internal readonly List<Action<MapperConfigurationExpression>> Actions = new();

    protected Profile()
    {
    }

    /// <summary>
    /// Register a mapping between TSource and TDestination. Can be chained with ForMember.
    /// </summary>
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var proxy = new DeferredMappingExpression<TSource, TDestination>();
        Actions.Add(cfg => proxy.Apply(cfg.CreateMap<TSource, TDestination>()));
        return proxy;
    }

    // Allows CreateMap<TSource,TDestination>() calls made in a Profile's constructor
    // (before the real MapperConfigurationExpression exists) to be replayed later
    // against the real configuration once the whole config is built.
    private class DeferredMappingExpression<TSource, TDestination> : IMappingExpression<TSource, TDestination>
    {
        private readonly List<Action<IMappingExpression<TSource, TDestination>>> _ops = new();

        public IMappingExpression<TSource, TDestination> ForMember<TMember>(
            System.Linq.Expressions.Expression<Func<TDestination, TMember>> destinationMember,
            Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions)
        {
            _ops.Add(real => real.ForMember(destinationMember, memberOptions));
            return this;
        }

        public IMappingExpression<TSource, TDestination> ForMember<TMember>(string destinationMember, Action<IMemberConfigurationExpression<TSource, TMember>> memberOptions)
        {
            _ops.Add(real => real.ForMember(destinationMember, memberOptions));
            return this;
        }

        public void Apply(IMappingExpression<TSource, TDestination> real)
        {
            foreach (var op in _ops) op(real);
        }

        
    }
}
