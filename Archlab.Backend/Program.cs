using Archlab.Backend;
using Archlab.Backend.Data;

var builder = WebApplication.CreateBuilder(args);

var serveFrontend = builder.Configuration.GetValue<bool>("SERVE_FRONTEND");

var app = ArchlabApi.Build(builder, serveStaticFiles: serveFrontend);

await DatabaseSeeder.SeedAsync(app.Services);

await app.RunAsync();
