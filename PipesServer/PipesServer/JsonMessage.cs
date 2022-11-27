using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipesServer
{
    public class JsonMessage
    {
        public string ClientName { get; set; }
        public string Message { get; set; }
        public string Time { get; set; }
    }
}
