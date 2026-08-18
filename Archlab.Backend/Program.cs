using Archlab.Backend;
using Archlab.Backend.Data;

var builder = WebApplication.CreateBuilder(args);

var app = ArchlabApi.Build(builder);

await DatabaseSeeder.SeedAsync(app.Services);

await app.RunAsync();
