using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var previewMode = builder.Configuration.GetValue<bool>("PreviewMode")
                  || string.Equals(Environment.GetEnvironmentVariable("IZMENJUJ_PREVIEW"), "1", StringComparison.Ordinal);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=Izmenjuj;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
var requireConfirmedAccount = previewMode
    ? false
    : builder.Configuration.GetValue("Identity:RequireConfirmedAccount", builder.Environment.IsProduction());

builder.Services.AddScoped<TradeNotificationInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
{
    if (previewMode)
    {
        options.UseSqlite("Data Source=izmenjuj-preview.db");
    }
    else
    {
        options.UseSqlServer(connectionString)
            .AddInterceptors(services.GetRequiredService<TradeNotificationInterceptor>());
    }
});
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = requireConfirmedAccount;
        options.Lockout.MaxFailedAccessAttempts = 6;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "izmenjuj.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() && !previewMode
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("write", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddScoped<UserSafetyFilter>();
builder.Services.AddControllersWithViews(options => options.Filters.AddService<UserSafetyFilter>());
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();
builder.Services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment() && !previewMode)
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!previewMode)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();

    if (previewMode)
    {
        await PreviewDataSeeder.SeedAsync(services);
    }
    else
    {
        await DatabaseBootstrap.InitializeAsync(db);

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var adminEmail = builder.Configuration["Admin:Email"]?.Trim();
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin is not null && !await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}

app.MapGet("/health", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var databaseOk = await db.Database.CanConnectAsync(cancellationToken);
    return databaseOk
        ? Results.Ok(new { status = "ok", app = "Izmenjuj.si", database = "ok", preview = previewMode })
        : Results.Problem(statusCode: 503, title: "Database unavailable");
});
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
