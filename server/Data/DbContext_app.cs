using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Server.Chat.Entitys;
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
        public DbSet<PaymentHistory> PaymentHistories { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<SocialAccount> SocialAccounts { get; set; }

        public DbSet<Conversation> Conversations { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<PostHistory> PostHistory { get; set; }
        
        public DbSet<PublishSession> PublishSessions { get; set; }

        public DbSet<Dealer> Dealers { get; set; }

        public DbSet<Car> Cars { get; set; }

        public DbSet<CarPhoto> CarPhotos { get; set; }

    }
}
