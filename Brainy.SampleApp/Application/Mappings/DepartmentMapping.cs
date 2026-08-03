using Brainy.Mapper;
using Brainy.Mapper.Interfaces;
using Brainy.SampleApp.Application.Models;
using Brainy.SampleApp.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brainy.SampleApp.Application.Mappings
{
    public class DepartmentMapping : IMap<Department>
    {
        public void Mapping(Profile profile)
        {
            profile.CreateMap<Department, DepartmentModel>();
        }
    }
}
