using Inviter.Server;
using Inviter.Server.Authorization;
using Inviter.Server.Models;
using Inviter.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Reflection;
using System.Text.Encodings.Web;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Literate, outputTemplate: "[{Timestamp:HH:mm:ss} | {Level:u3} | {SourceContext}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

var inviterSettings = builder.Configuration.GetRequiredSection(nameof(Inviter)).Get<InviterSettings>();
// Add services to the container.

builder.Host.UseSerilog();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

builder.Services.AddHttpClient();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(inviterSettings!);
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<ISoriginService, SoriginService>();
builder.Services.AddSingleton(Assembly.GetExecutingAssembly().GetName().Version!);
builder.Services.AddSingleton<IAuthorizationHandler, InviterStateAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, InviterAuthorizationPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, InviterAuthorizationMiddlewareResultHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => o.Cookie.Name = "inviter.session.cookie");
builder.Services.AddDbContext<InviterContext>(o => { o.UseNpgsql(builder.Configuration.GetConnectionString("Default"), o => o.UseNodaTime()); o.UseSnakeCaseNamingConvention(); });

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseRouting();
app.UseCookiePolicy(new() { MinimumSameSitePolicy = SameSiteMode.None });
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.Map("/api", async (context) =>
    {
        Version version = context.RequestServices.GetRequiredService<Version>();
        string versionText = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        await context.Response.WriteAsJsonAsync(new { status = "HEALTHY", version = versionText });
    });
});

IServiceScope scope = app.Services.CreateScope();
var provider = scope.ServiceProvider;
var inviterContext = provider.GetRequiredService<InviterContext>();
try { await inviterContext.Database.MigrateAsync(); } catch { }
scope.Dispose();

app.Run();
