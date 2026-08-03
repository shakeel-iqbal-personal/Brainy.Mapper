using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brainy.SampleApp.DependencyInjection.Application.Models
{
    public class CreateStudentModel
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public int DepartmentId { get; set; }
    }
}
