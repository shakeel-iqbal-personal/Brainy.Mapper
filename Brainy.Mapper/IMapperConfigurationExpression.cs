namespace Brainy.Mapper;

public interface IMapperConfigurationExpression
{
     IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();
}

