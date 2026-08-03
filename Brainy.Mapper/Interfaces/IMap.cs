namespace Brainy.Mapper.Interfaces;

public interface IMap<in T>
{
    void Mapping(Profile profile);
}
