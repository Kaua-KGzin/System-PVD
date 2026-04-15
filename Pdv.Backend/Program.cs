using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Data;
using Pdv.Backend.Endpoints;
using Pdv.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<PdvDbContext>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<PdvDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PdvDatabase")
        ?? "Data Source=pdv.db";

    options.UseSqlite(connectionString);
});

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CashRegisterService>();
builder.Services.AddScoped<FiscalDocumentService>();
builder.Services.AddScoped<SaleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapHealthChecks("/health");
app.MapProductsEndpoints();
app.MapCashRegisterEndpoints();
app.MapSalesEndpoints();
app.MapFiscalDocumentEndpoints();

await DatabaseSeeder.SeedAsync(app.Services);

await app.RunAsync();
