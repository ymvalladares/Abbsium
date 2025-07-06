using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Server.Entitys;

namespace Server.Data
{
    public class DbContext_app : IdentityDbContext
    {
        public DbContext_app(DbContextOptions<DbContext_app> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.LogTo(Console.WriteLine);

        public DbSet<User_data> User_Data { get; set; }
        public DbSet<Order> Order { get; set; }
    }
}
