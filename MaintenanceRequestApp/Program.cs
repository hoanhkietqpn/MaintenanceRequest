using MaintenanceRequestApp.Data;
using MaintenanceRequestApp.Hubs;
using MaintenanceRequestApp.Services;
using MaintenanceRequestApp.Filters;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Configure EF Core with PostgreSQL
builder.Services.AddDbContext<MaintenanceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Configure 20MB limit for multipart body length
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20 MB
});

// 3. Register ImageProcessingService
// Ensure WebP conversion service is available
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

// 3.5 Register EmailService
builder.Services.AddScoped<IEmailService, EmailService>();

// 3.6 Register Reminder Service for Hangfire
builder.Services.AddScoped<IReminderService, ReminderService>();

// 4. Configure Authentication & Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(3);
    });

// 5. HttpClient for External API Login
builder.Services.AddHttpClient();

// 6. Config SignalR 
builder.Services.AddSignalR();

// 7. Configure Hangfire with PostgreSQL
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Add the Hangfire processing server as IHostedService
builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// 1. Default static files from wwwroot
app.UseStaticFiles();

// 2. Custom static files for external uploads
var uploadSettings = builder.Configuration.GetSection("FileUploadSettings");
var physicalPath = uploadSettings["PhysicalPath"] ?? "ExternalUploads";
var webPath = uploadSettings["WebPath"] ?? "/uploads";

// Ensure physical path is absolute
if (!Path.IsPathRooted(physicalPath))
{
    physicalPath = Path.Combine(builder.Environment.ContentRootPath, physicalPath);
}

// Ensure directory exists
if (!Directory.Exists(physicalPath))
{
    Directory.CreateDirectory(physicalPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(physicalPath),
    RequestPath = webPath
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 8. Map Hangfire Dashboard with custom authorization
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "VIAA Maintenance - Hangfire"
});

app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Request}/{action=Create}/{id?}");

app.Run();
