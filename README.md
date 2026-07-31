# Brainy.Mapper

Brainy.Mapper is a lightweight, high-performance object mapping library for .NET designed as a simple and efficient alternative to traditional object mappers.

The project was created to provide developers with a free, fast, and easy-to-use mapping solution without licensing concerns. It supports mapping between objects, nested objects, collections, custom member mappings, and convention-based property mapping while keeping the API clean and familiar.

## Features

- 🚀 High-performance object mapping
- 🔄 Convention-based property mapping
- 🎯 Custom member mapping with `ForMember`
- 📦 Nested object mapping
- 📋 Collection and List mapping
- 🔧 Configurable mapping profiles
- 💪 Strongly typed lambda expressions
- 🪶 Lightweight with zero unnecessary dependencies
- 🆓 Completely free and open source

## Why Brainy.Mapper?

Many existing mapping libraries have become commercial or include features that many projects never use. Brainy.Mapper focuses on providing the core functionality developers need while remaining lightweight, performant, and easy to understand.

Whether you're building a small application or a large enterprise solution, Brainy.Mapper aims to provide a clean and reliable mapping experience.

## Example

```csharp
var profile = new MapperProfile();

profile.CreateMap<User, UserDto>()
       .ForMember(dest => dest.FullName,
           opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));

var mapper = new Mapper(profile);

UserDto dto = mapper.Map<UserDto>(user);
```

## Roadmap

- [x] Object mapping
- [x] Nested object mapping
- [x] Collection mapping
- [x] Custom member mapping
- [x] Enum mapping
- [x] Dependency Injection extensions
- [ ] Reverse mapping
- [ ] Attribute-based mapping
- [ ] Constructor mapping
