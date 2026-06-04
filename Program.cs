using Microsoft.AspNetCore.Authentication.Cookies;
using TicketSistemi.Hubs;
using TicketSistemi.Data;
using Microsoft.EntityFrameworkCore;
using TicketSistemi.Utils;
using TicketSistemi.Jobs;

var builder = WebApplication.CreateBuilder(args);

// Configure Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFile(Path.Combine(builder.Environment.ContentRootPath, "Logs", "app.log"));

// Get connection string from various possible sources to ensure compatibility on Render
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? Environment.GetEnvironmentVariable("DefaultConnection");

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Automatically detect if we are using PostgreSQL based on connection string prefix (works even in Development mode on Render)
    if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
    {
        var npgsqlConnectionString = ConvertPostgresUrlToConnectionString(connectionString);
        options.UseNpgsql(npgsqlConnectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=ticket.db");
    }
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHostedService<AutoCloseTicketsJob>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

var app = builder.Build();

// Automatically apply migrations/ensure database created and seed admin user on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
    {
        // Use EnsureCreated for PostgreSQL (database-agnostic, doesn't require provider-specific migrations)
        dbContext.Database.EnsureCreated();
    }
    else
    {
        // Use standard migration for SQLite in development
        dbContext.Database.Migrate();
    }

    if (!await dbContext.Users.AnyAsync())
    {
        dbContext.Users.Add(new TicketSistemi.Models.User
        {
            Username = "admin",
            PasswordHash = PasswordHelper.HashPassword("admin", "123"),
            Role = "Admin",
            CreatedDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Ticket}/{action=Index}/{id?}");

app.Run();

// Helper function to convert Render/Neon PostgreSQL URL to Npgsql compatible connection string
static string ConvertPostgresUrlToConnectionString(string url)
{
    if (string.IsNullOrEmpty(url) || (!url.StartsWith("postgres://") && !url.StartsWith("postgresql://")))
    {
        return url;
    }

    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port;
    var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
}
