using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Chat.HubFolder;
using Server.Data;
using Server.Entitys;
using Server.Helpers;
using Server.Middleware;
using Server.Repositories;
using Server.Repositories.IRepositories;
using Stripe;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var serverVersion = new MySqlServerVersion(new Version(8, 0, 29));

/* =========================
   DEPENDENCY INJECTION
========================= */

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, Server.Services.TokenService>();
builder.Services.AddScoped<IChatService, Server.Services.ChatService>();

builder.Services.AddAutoMapper(typeof(AutoMapperProfiles).Assembly);

/* =========================
   DATABASE
========================= */

builder.Services.AddDbContext<DbContext_app>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        serverVersion
    )
);

/* =========================
   IDENTITY
========================= */

builder.Services.AddIdentity<User_data, IdentityRole>()
    .AddEntityFrameworkStores<DbContext_app>()
    .AddDefaultTokenProviders();

/* =========================
   JWT AUTH
========================= */

var key = Encoding.ASCII.GetBytes(builder.Configuration["JwtSettings:Key"]);

var tokenValidationParams = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(key),
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateLifetime = true,
    RequireExpirationTime = false,
    ClockSkew = TimeSpan.Zero
};

builder.Services.AddSingleton(tokenValidationParams);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(jwt =>
{
    jwt.SaveToken = true;
    jwt.TokenValidationParameters = tokenValidationParams;

    // ? IMPORTANTE PARA SIGNALR CON JWT
    jwt.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

/* =========================
   CORS (OBLIGATORIO PARA SIGNALR)
========================= */

builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatCorsPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:5174",
                "https://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

/* =========================
   SIGNALR
========================= */

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

/* =========================
   CONTROLLERS & SWAGGER
========================= */

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

/* =========================
   SEED ADMIN USER
========================= */

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var configuration = services.GetRequiredService<IConfiguration>();

    await Server.Services.DataSeeder
        .SeedRolesAndAdminAsync(services, configuration);
}

/* =========================
   MIDDLEWARE PIPELINE
========================= */

app.UseSwagger();
app.UseSwaggerUI();

app.UseStatusCodePages();
app.UseMiddleware<GeneralMidleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

/* ? AQUI ESTABA EL ERROR */
app.UseCors("ChatCorsPolicy");

StripeConfiguration.ApiKey =
    builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();

app.UseAuthentication();
app.UseAuthorization();

/* =========================
   ENDPOINTS
========================= */

app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat")
   .RequireCors("ChatCorsPolicy");

app.Run();
