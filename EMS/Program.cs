using EMS;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using EMS.Controllers;
using EMS.Data;
using EMS.Models.Landing;
using EMS.Services;
using EMS.Services.Auth;
using EMS.Services.Demo;
using EMS.Services.Email;

var builder = WebApplication.CreateBuilder(args);

// "Today" for attendance, leave and the demo reset is the organization's date, not the server's.
AppClock.Configure(builder.Configuration["App:TimeZone"]);

// Keep the keys that protect sign-in cookies and forms in App_Data, so a restart of the shared IIS app pool does not
// sign everyone out.
builder.Services.AddDataProtection()
    .SetApplicationName("EMS")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddEmsServices();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddImpersonation();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(NotDemoPolicy, policy => policy.RequireAssertion(context => !context.User.HasClaim(c => c.Type == DemoSeeder.DemoClaim)));
// The demo logins are shared: visitors must not change their password, email or account.
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage", NotDemoPolicy));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.Section));
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddHostedService<DemoResetService>();
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection(SuperAdminOptions.Section));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddControllersWithViews();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection(CompanyOptions.Section));

// Public enquiry form: at most 5 submissions per IP every 10 minutes.
// Behind a reverse proxy, enable UseForwardedHeaders so RemoteIpAddress is the visitor's IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(HomeController.EnquiryRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        return new ValueTask(context.HttpContext.Response.WriteAsync(
            "Too many enquiries from your network. Please try again in a few minutes.", ct));
    };
});

// Outside development, http:// redirects permanently, so search engines index only the https address.
if (!builder.Environment.IsDevelopment())
    builder.Services.AddHttpsRedirection(options => options.RedirectStatusCode = StatusCodes.Status301MovedPermanently);

var app = builder.Build();

await app.Services.SeedIdentityAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program
{
    private const string NotDemoPolicy = "NotDemo";
}
