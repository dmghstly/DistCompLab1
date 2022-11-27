using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace PipesServer
{
    public class MessagesDBContext : DbContext
    {
        public MessagesDBContext() : base("DefaultConnection")
        {
            Database.Initialize(force: false);
        }

        public DbSet<ClientMessage> ClientMessages { get; set; }
        public DbSet<Client> Users { get; set; }
    }
}
