using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Archlab.Backend.Data;
using Archlab.Backend.Endpoints;
using Archlab.Backend.Services;
using Archlab.Backend.Services.Settings;
using Serilog;
using Serilog.Events;

// Bootstrap logger for startup errors (replaced after host is built)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging — replaces the bootstrap logger after config is loaded
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithEnvironmentName()
           .WriteTo.Console(
               outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

        if (!ctx.HostingEnvironment.IsDevelopment())
        {
            // JSON output for log aggregators (Datadog, ELK, CloudWatch) in production
            cfg.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
        }
    });

    // Typed configuration — validated at startup, no runtime KeyNotFoundException
    builder.Services
        .AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection(JwtSettings.Section))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    var jwtSettings = builder.Configuration.GetSection(JwtSettings.Section).Get<JwtSettings>()
        ?? throw new InvalidOperationException("Secao Jwt nao configurada.");

    if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
        throw new InvalidOperationException("Configure Jwt:SecretKey via user secrets, variaveis de ambiente ou appsettings.Development.json.");

    if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
        throw new InvalidOperationException("Jwt:SecretKey precisa ter no minimo 32 bytes para HMAC-SHA256.");

    // Trust X-Forwarded-For from reverse proxies (nginx, AWS ALB, Cloudflare).
    // Without this, RemoteIpAddress is always the proxy IP → rate limiting ineffective for anonymous users.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Accept X-Forwarded-For from any source.
        // In production, restrict this to your known proxy CIDR via KnownIPNetworks.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }
            else if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            }
            else
            {
                throw new InvalidOperationException("Configure Cors:AllowedOrigins antes de executar fora do ambiente Development.");
            }
        });
    });

    builder.Services.AddRateLimiter(options =>
    {
        // Global limiter: 100 req/min per authenticated user (by JWT sub) or per real client IP
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "authenticated"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Login endpoint: strict limit to mitigate brute-force attacks
        options.AddFixedWindowLimiter("login", loginOptions =>
        {
            loginOptions.PermitLimit = 5;
            loginOptions.Window = TimeSpan.FromMinutes(1);
            loginOptions.QueueLimit = 0;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks().AddDbContextCheck<PdvDbContext>();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

    // Database
    var dbProvider = builder.Configuration["DatabaseProvider"]
        ?? (builder.Environment.IsDevelopment()
            ? "Sqlite"
            : throw new InvalidOperationException("DatabaseProvider precisa ser configurado em producao (PostgreSQL ou Sqlite)."));

    builder.Services.AddDbContext<PdvDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(connectionString ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection e obrigatoria para PostgreSQL."));
        }
        else
        {
            options.UseSqlite(connectionString ?? "Data Source=archlab.db");
        }
    });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                NameClaimType = JwtRegisteredClaimNames.UniqueName,
                RoleClaimType = ClaimTypes.Role
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("AdminOrManager", policy => policy.RequireRole("Admin", "Manager"));
    });

    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });

    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<ProductService>();
    builder.Services.AddScoped<CategoryService>();
    builder.Services.AddScoped<CustomerService>();
    builder.Services.AddScoped<CashRegisterService>();
    builder.Services.AddScoped<FiscalDocumentService>();
    builder.Services.AddScoped<SaleService>();
    builder.Services.AddScoped<ReportService>();
    builder.Services.AddScoped<SupplierService>();
    builder.Services.AddScoped<PurchaseEntryService>();

    var app = builder.Build();

    // Must be first — rewrites RemoteIpAddress before any middleware reads it
    app.UseForwardedHeaders();

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

    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        // API-only server: CSP restricts to 'none' — this backend does not serve HTML pages.
        // The frontend SPA must set its own CSP on the HTML server (Vite/nginx/CDN).
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        await next();
    });

    // Request logging via Serilog (replaces the default HTTP logging middleware)
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondido {StatusCode} em {Elapsed:0.0000}ms";
        opts.GetLevel = (httpCtx, elapsed, ex) =>
            ex is not null || httpCtx.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : httpCtx.Response.StatusCode >= 400
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseCors();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapAuthEndpoints();
    app.MapProductsEndpoints();
    app.MapCategoryEndpoints();
    app.MapCustomerEndpoints();
    app.MapCashRegisterEndpoints();
    app.MapSalesEndpoints();
    app.MapFiscalDocumentEndpoints();
    app.MapReportEndpoints();
    app.MapDashboardEndpoints();
    app.MapSupplierEndpoints();
    app.MapPurchaseEntryEndpoints();
    app.MapUserEndpoints();

    await DatabaseSeeder.SeedAsync(app.Services);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Aplicacao encerrada inesperadamente durante o startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
