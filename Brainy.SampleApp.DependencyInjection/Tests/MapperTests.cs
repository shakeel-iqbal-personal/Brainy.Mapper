using Brainy.Mapper;
using Brainy.SampleApp.DependencyInjection.Application.Models;
using Brainy.SampleApp.DependencyInjection.Domain;

namespace Brainy.SampleApp.DependencyInjection.Tests;

public class MapperTests
{
    private readonly IMapper _mapper;

    public MapperTests(IMapper mapper)
    {
        _mapper = mapper;
    }

    public void Run()
    {
        var department = BuildData();

        Test1(department);
        Test2(department);
        Test3(department);
        Test4();
        Test5(department);
        Test6(department);
        Test7();
        Test8(department);
        Test9(department);
        Test10(department);
    }

    private Department BuildData()
    {
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
            DepartmentId = 1,
            Department = department
        });

        department.Students.Add(new Student
        {
            Id = 2,
            Name = "Ahmed",
            Age = 22,
            DepartmentId = 1,
            Department = department
        });

        return department;
    }

    private void Test1(Department department)
    {
        Console.WriteLine("===== Test 1 =====");

        var student = department.Students.First();

        var model = _mapper.Map<StudentModel>(student);

        Console.WriteLine(model.Id);
        Console.WriteLine(model.Name);
        Console.WriteLine(model.Age);
    }

    private void Test2(Department department)
    {
        Console.WriteLine("===== Test 2 =====");

        var student = department.Students.First();

        var model = _mapper.Map<StudentModel>(student);

        Console.WriteLine(model.DepartmentName);
    }

    private void Test3(Department department)
    {
        Console.WriteLine("===== Test 3 =====");

        var model = _mapper.Map<DepartmentModel>(department);

        Console.WriteLine(model.Name);

        foreach (var student in model.Students)
        {
            Console.WriteLine(student.Name);
        }
    }

    private void Test4()
    {
        Console.WriteLine("===== Test 4 =====");

        var create = new CreateStudentModel
        {
            Name = "John",
            Age = 25,
            DepartmentId = 1
        };

        var entity = _mapper.Map<Student>(create);

        Console.WriteLine(entity.Name);
        Console.WriteLine(entity.Age);
        Console.WriteLine(entity.DepartmentId);
    }

    private void Test5(Department department)
    {
        Console.WriteLine("===== Test 5 =====");

        var student = department.Students.First();

        var update = new UpdateStudentModel
        {
            Name = "Updated Ali",
            Age = 30
        };

        _mapper.Map(update, student);

        Console.WriteLine(student.Name);
        Console.WriteLine(student.Age);
    }

    private void Test6(Department department)
    {
        Console.WriteLine("===== Test 6 =====");

        var models = _mapper.Map<List<StudentModel>>(department.Students);

        foreach (var model in models)
        {
            Console.WriteLine(model.Name);
        }
    }

    private void Test7()
    {
        Console.WriteLine("===== Test 7 =====");

        Student student = null;

        var model = _mapper.Map<StudentModel>(student);

        Console.WriteLine(model == null);
    }

    private void Test8(Department department)
    {
        Console.WriteLine("===== Test 8 =====");

        var student = department.Students.First();

        var existing = new StudentModel
        {
            Id = 99,
            Name = "Old",
            Age = 5
        };

        _mapper.Map(student, existing);

        Console.WriteLine(existing.Name);
        Console.WriteLine(existing.Age);
    }

    private void Test9(Department department)
    {
        Console.WriteLine("===== Test 9 =====");

        var projected = _mapper.ProjectTo<StudentModel>(
            department.Students.AsQueryable());

        foreach (var item in projected)
        {
            Console.WriteLine(item.Name);
        }
    }

    private void Test10(Department department)
    {
        Console.WriteLine("===== Test 10 =====");

        var student = department.Students.First();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 100000; i++)
        {
            _mapper.Map<StudentModel>(student);
        }

        sw.Stop();

        Console.WriteLine(sw.ElapsedMilliseconds + " ms");
    }
}