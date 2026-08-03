using Brainy.Mapper.DependencyInjection;
using Brainy.SampleApp.DependencyInjection.Tests;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

services.AddBrainyMapper(Assembly.GetExecutingAssembly());

services.AddSingleton<MapperTests>();

var provider = services.BuildServiceProvider();

provider.GetRequiredService<MapperTests>().Run();