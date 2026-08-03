using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brainy.SampleApp.DependencyInjection.Application.Models
{
    public class DepartmentModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<StudentModel> Students { get; set; } = new();
    }
}
