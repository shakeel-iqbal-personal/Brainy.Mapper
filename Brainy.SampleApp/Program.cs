using Brainy.Mapper;
using Brainy.SampleApp.Application.Common.Mappings;
using Brainy.SampleApp.Application.Models;
using Brainy.SampleApp.Domain;
using System.Diagnostics;
using System.Reflection;

var profile = new MappingProfile(Assembly.GetExecutingAssembly());

var config = new MapperConfiguration(cfg =>
{
    cfg.AddProfile(profile);
});

var mapper = config.CreateMapper();

var department = new Department
{
    Id = 1,
    Name = "Computer Science"
};

department.Students.Add(new Student
{
    Id = 1,
    Name = "Ali",
    Age = 20,
    DepartmentId = department.Id,
    Department = department
});

department.Students.Add(new Student
{
    Id = 2,
    Name = "Ahmed",
    Age = 22,
    DepartmentId = department.Id,
    Department = department
});


var student = department.Students.First();

var model = mapper.Map<StudentModel>(student);

Console.WriteLine("===== Test 1 =====");
Console.WriteLine(model.Id);
Console.WriteLine(model.Name);
Console.WriteLine(model.Age);

Console.WriteLine("===== Test 2 =====");
Console.WriteLine(model.DepartmentName);



Console.WriteLine("===== Test 3 =====");
var depModel = mapper.Map<DepartmentModel>(department);

Console.WriteLine(depModel.Name);

foreach (var s in depModel.Students)
{
    Console.WriteLine(s.Name);
}

Console.WriteLine("===== Test 4 =====");

var create = new CreateStudentModel
{
    Name = "John",
    Age = 25,
    DepartmentId = 1
};

var entity = mapper.Map<Student>(create);

Console.WriteLine(entity.Name);
Console.WriteLine(entity.Age);
Console.WriteLine(entity.DepartmentId);

Console.WriteLine("===== Test 5 =====");

var update = new UpdateStudentModel
{
    Name = "Updated Ali",
    Age = 30
};

mapper.Map(update, student);

Console.WriteLine(student.Name);

Console.WriteLine(student.Age);

Console.WriteLine("===== Test 6 =====");

var students = department.Students;

var models = mapper.Map<List<StudentModel>>(students);

foreach (var item in models)
{
    Console.WriteLine(item.Name);
}

Console.WriteLine("===== Test 7 =====");

Student student2 = null;

var dto = mapper.Map<StudentModel>(student2);

Console.WriteLine(dto == null);


Console.WriteLine("===== Test 8 =====");
var existing = new StudentModel
{
    Id = 99,
    Name = "Old",
    Age = 5
};

mapper.Map(student, existing);

Console.WriteLine(existing.Name);

Console.WriteLine(existing.Age);


Console.WriteLine("===== Test 9 =====");

var query = department.Students.AsQueryable();

var projected = mapper.ProjectTo<StudentModel>(query);

foreach (var item in projected)
{
    Console.WriteLine(item.Name);
}


Console.WriteLine("===== Test 10 =====");
var stopwatch = Stopwatch.StartNew();

for (int i = 0; i < 100000; i++)
{
    mapper.Map<StudentModel>(student);
}

stopwatch.Stop();

Console.WriteLine(stopwatch.ElapsedMilliseconds);


