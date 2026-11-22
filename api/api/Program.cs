using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using OpsPilot.Api.Extensions;
using OpsPilot.Application;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------- Kestrel (server web bawaan) ----------
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;               // jangan umumkan "Server: Kestrel"
    options.Limits.MaxRequestBodySize = 1_000_000; // 1 MB: permintaan terbesar kita hanya belasan KB
});

// ---------- Logging ----------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------- Mendaftarkan service dari tiap layer ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// ---------- Header keamanan untuk SEMUA respons ----------
// OnStarting dijalankan tepat sebelum respons dikirim, jadi header ikut terpasang juga pada respons error.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff"; // browser tidak boleh menebak jenis isi
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cache-Control"] = "no-store"; // token dan data pribadi tidak boleh disimpan cache
        return Task.CompletedTask;
    });

    await next();
});

// ---------- Middleware (URUTAN PENTING) ----------
app.UseSerilogRequestLogging();
app.UseCors(ServiceCollectionExtensions.CorsPolicyName); // sebelum penangan error: respons error tetap membawa header CORS
app.UseExceptionHandler();
app.UseStatusCodePages();

var isDevelopment = app.Environment.IsDevelopment();

// Swagger: Development, atau dinyalakan eksplisit (stack demo)
if (isDevelopment || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------- Basis data: migration dan data demo ----------
// Development: otomatis. Lingkungan lain: HANYA kalau dinyalakan lewat pengaturan.
// Data demo berisi akun dengan password yang diketahui publik, jadi tidak pernah menyala sendiri di luar Development.
var seedDemoData = isDevelopment || app.Configuration.GetValue<bool>("Database:SeedDemoData");
var migrate = seedDemoData || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");

if (seedDemoData && !isDevelopment)
    app.Logger.LogWarning("Demo accounts are being seeded outside Development. Use this for local demos only.");

if (migrate)
    await app.Services.InitialiseDatabaseAsync(seedDemoData);

app.UseRateLimiter(); // setelah CORS: respons 429 tetap terbaca oleh browser
app.UseAuthentication();
app.UseAuthorization();

// ---------- Endpoint ----------
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
    }
}).AllowAnonymous();

app.Run();

// Dibutuhkan agar project test bisa memanggil aplikasi ini
public partial class Program { }