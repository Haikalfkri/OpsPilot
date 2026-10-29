using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using OpsPilot.Api.Extensions;
using OpsPilot.Application;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

// ---------- Middleware (URUTAN PENTING) ----------
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Hanya Development: buat tabel + isi data demo
    await app.Services.InitialiseDatabaseAsync(seedDemoData: true);
}

app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
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