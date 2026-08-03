using Brainy.Mapper;
using Brainy.Mapper.Interfaces;
using Brainy.SampleApp.DependencyInjection.Application.Models;
using Brainy.SampleApp.DependencyInjection.Domain;


namespace Brainy.SampleApp.Application.Mappings;

public class StudentMapping : IMap<Student>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Student, StudentModel>()

            .ForMember(
                d => d.DepartmentName,
                opt => opt.MapFrom(s => s.Department.Name));

        profile.CreateMap<CreateStudentModel, Student>();

        profile.CreateMap<UpdateStudentModel, Student>();
    }
}