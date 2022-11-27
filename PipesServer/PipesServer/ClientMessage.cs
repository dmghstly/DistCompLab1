using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace PipesServer
{
    public class ClientMessage
    {
        [Key]
        public int Id { get; set; }
        public string MessageContent { get; set; }
        public string Time { get; set; }

        public string UserName { get; set; }
    }
}
