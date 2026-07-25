using Microsoft.AspNetCore.Identity;
using System;

namespace Server.Entitys
{
    public class User_data : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
