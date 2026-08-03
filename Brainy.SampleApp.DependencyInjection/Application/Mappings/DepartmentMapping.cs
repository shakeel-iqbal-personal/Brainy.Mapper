using Brainy.Mapper;
using Brainy.Mapper.Interfaces;
using Brainy.SampleApp.DependencyInjection.Application.Models;
using Brainy.SampleApp.DependencyInjection.Domain;


namespace Brainy.SampleApp.DependencyInjection.Mappings;

public class DepartmentMapping : IMap<Department>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Department, DepartmentModel>();
    }
}