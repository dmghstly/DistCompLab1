using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipesServer
{
    public class Client
    {
        [Key]
        public Guid Id { get; set; }
        public string UserName { get; set; }
    }
}
