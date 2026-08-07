# Brainy.Mapper

Brainy.Mapper is a lightweight and high-performance object mapping library for .NET 8, 9 & 10.

It was built to provide a clean, fast, and dependency-light alternative for mapping objects without unnecessary complexity. Whether you're mapping DTOs, entities, nested models, or query projections, Brainy.Mapper keeps the API simple while covering the features commonly required in real-world applications.

The library follows familiar mapping conventions, making it easy to adopt while remaining lightweight and fully open source.

---

## Features

- 🚀 Fast object-to-object mapping
- 🎯 Convention-based property mapping
- 🔄 Custom member mapping with `ForMember()`
- 📦 Nested object mapping
- 📋 Collection and array mapping
- 🌳 Automatic property flattening
- 🚫 Ignore destination members
- ✏️ Map onto existing objects
- 🔢 Automatic enum conversion
- 📈 LINQ `ProjectTo()` support
- 💉 Dependency Injection support
- 🧩 Automatic mapping registration using `IMap<T>`
- 🛡️ Null-safe projections
- ⚙️ Configurable mapping profiles
- 🪶 Lightweight with minimal dependencies
- 🆓 Free and open source

---

# Why Brainy.Mapper?

Brainy.Mapper was created with one goal in mind:

> Provide the mapping features developers actually use without adding unnecessary complexity.

Many mapping libraries have grown significantly over time, introducing features that aren't needed in every project or moving toward commercial licensing. Brainy.Mapper focuses on delivering the core mapping experience in a clean, maintainable, and developer-friendly way.

Whether you're building a small API, a Clean Architecture application, or a large enterprise solution, Brainy.Mapper helps reduce repetitive mapping code while keeping configuration straightforward and easy to understand.

---

# Installation

Install the package from NuGet.

```bash
dotnet add package Brainy.Mapper
```

or

```powershell
Install-Package Brainy.Mapper
```

---

# Quick Start

## Create a Profile

```csharp
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(
                d => d.FullName,
                opt => opt.MapFrom(s => s.FirstName + " " + s.LastName));
    }
}
```

## Configure the Mapper

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<UserProfile>();
});

var mapper = configuration.CreateMapper();
```

## Map Objects

```csharp
UserDto dto = mapper.Map<UserDto>(user);
```

---

# Dependency Injection

Brainy.Mapper includes built-in Dependency Injection support.

```csharp
builder.Services.AddBrainyMapper(
    Assembly.GetExecutingAssembly());
```

Then simply inject `IMapper`.

```csharp
public class UserService
{
    private readonly IMapper _mapper;

    public UserService(IMapper mapper)
    {
        _mapper = mapper;
    }
}
```

---

# Automatic Mapping Registration

Brainy.Mapper can automatically discover mappings from your assembly using the `IMap<T>` interface.

```csharp
public class UserMapping : IMap<User>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<User, UserDto>();
    }
}
```

No manual registration of every mapping class is required.

---

# Collection Mapping

```csharp
List<UserDto> users =
    mapper.Map<List<UserDto>>(entities);
```

---

# Nested Object Mapping

```csharp
public class Department
{
    public string Name { get; set; }
}

public class Student
{
    public Department Department { get; set; }
}
```

```csharp
public class StudentDto
{
    public DepartmentDto Department { get; set; }
}
```

Nested objects are mapped automatically when a mapping exists for both types.

---

# Property Flattening

Brainy.Mapper automatically supports property flattening.

```csharp
public class Student
{
    public Department Department { get; set; }
}
```

```csharp
public class StudentDto
{
    public string DepartmentName { get; set; }
}
```

No additional configuration is required.

---

# Custom Member Mapping

```csharp
CreateMap<User, UserDto>()
    .ForMember(
        d => d.FullName,
        opt => opt.MapFrom(
            s => s.FirstName + " " + s.LastName));
```

---

# Ignore Members

```csharp
CreateMap<User, UserDto>()
    .ForMember(
        d => d.Password,
        opt => opt.Ignore());
```

---

# Mapping to Existing Objects

```csharp
mapper.Map(updateRequest, existingEntity);
```

Useful for update operations where an existing entity should be modified instead of creating a new instance.

---

# ProjectTo

Project directly from an `IQueryable` without materializing entities first.

```csharp
var users = context.Users
    .ProjectTo<UserDto>(mapper)
    .ToList();
```

This allows only the required columns to be selected by the underlying query provider.

---

# Roadmap

- ✅ Object Mapping
- ✅ Nested Object Mapping
- ✅ Collection Mapping
- ✅ Property Flattening
- ✅ Custom Member Mapping
- ✅ Ignore Members
- ✅ Existing Object Mapping
- ✅ Enum Conversion
- ✅ Dependency Injection
- ✅ Assembly Scanning
- ✅ ProjectTo Support
- ⏳ Reverse Mapping
- ⏳ Constructor Mapping
- ⏳ Attribute-Based Mapping

---

# Contributing

Contributions, bug reports, feature suggestions, and pull requests are always welcome.

If you find an issue or have an idea for improvement, feel free to open an issue on GitHub.

---

# Author

Brainy.Mapper is created and maintained by **Shakeel Iqbal**, a Senior .NET Architect and C# Developer with extensive experience building enterprise applications and software solutions using the Microsoft technology stack.

- LinkedIn: [Shakeel Iqbal](https://www.linkedin.com/in/shakeel-iqbal1/)
- Company: [Brainy Solutions](https://www.brainy-solutions.com/)

If you find Brainy.Mapper useful, feel free to ⭐ star the repository, report issues, suggest improvements, or contribute through a pull request.

---

# License

Brainy.Mapper is released under the MIT License.