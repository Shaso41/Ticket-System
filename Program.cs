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

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(connectionString ?? "Data Source=ticket.db");
    }
    else
    {
        options.UseNpgsql(connectionString);
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
    
    if (app.Environment.IsDevelopment())
    {
        // Use standard migration for SQLite in development
        dbContext.Database.Migrate();
    }
    else
    {
        // Use EnsureCreated for PostgreSQL in production (database-agnostic, doesn't require provider-specific migrations)
        dbContext.Database.EnsureCreated();
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
