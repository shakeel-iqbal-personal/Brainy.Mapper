using Brainy.Mapper;
using Brainy.Mapper.Interfaces;
using Brainy.SampleApp.Application.Models;
using Brainy.SampleApp.Domain;

namespace Brainy.SampleApp.Application.Mappings;

public class StudentMapping : IMap<Student>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Student, StudentModel>();

        profile.CreateMap<CreateStudentModel, Student>();

        profile.CreateMap<UpdateStudentModel, Student>();
    }
}