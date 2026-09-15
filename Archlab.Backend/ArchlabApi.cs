using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Archlab.Backend.Data;
using Archlab.Backend.Data.Interceptors;
using Archlab.Backend.Endpoints;
using Archlab.Backend.Hardware;
using Archlab.Backend.Services;

namespace Archlab.Backend;

/// <summary>
/// The complete API composition, callable by any host. The web deployment calls it from
/// Program.cs; the desktop shell calls it in-process so a cashier terminal runs one process
/// instead of a UI plus an orphanable child server.
/// </summary>
public static class ArchlabApi
{
    /// <param name="httpsRedirection">
    /// False for a host that serves plain HTTP on loopback only — the desktop shell — where a
    /// redirect to a nonexistent HTTPS endpoint would break every request.
    /// </param>
    /// <param name="serveStaticFiles">
    /// True to serve the SPA from the host's web root and fall back to index.html, so the API
    /// and the UI share one origin. The container deployment serves the API alone.
    /// </param>
    public static WebApplication Build(
        WebApplicationBuilder builder,
        bool httpsRedirection = true,
        bool serveStaticFiles = false)
    {
        var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtSecretKey))
        {
            throw new InvalidOperationException("Configure Jwt:SecretKey via user secrets, environment variables, or appsettings.Development.json.");
        }

        if (Encoding.UTF8.GetByteCount(jwtSecretKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes for HMAC-SHA256.");
        }

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
                else if (serveStaticFiles)
                {
                    // Single-origin host: the UI is served by this app, so no cross-origin
                    // caller is expected and the default policy allows none.
                }
                else
                {
                    throw new InvalidOperationException("Configure Cors:AllowedOrigins before running outside Development.");
                }
            });
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? httpContext.User.Identity.Name ?? "authenticated"
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
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks().AddDbContextCheck<PdvDbContext>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditLogInterceptor>();
        builder.Services.AddValidatorsFromAssemblyContaining(typeof(ArchlabApi));

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        var forceSqlite = builder.Configuration["DatabaseProvider"]?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        builder.Services.AddDbContext<PdvDbContext>((sp, options) =>
        {
            var auditInterceptor = sp.GetRequiredService<AuditLogInterceptor>();
            options.AddInterceptors(auditInterceptor);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            // PostgreSQL como principal; fallback para SQLite se a connection string não estiver configurada
            if (!string.IsNullOrWhiteSpace(connectionString) && !forceSqlite)
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(forceSqlite && !string.IsNullOrWhiteSpace(connectionString) 
                    ? connectionString 
                    : "Data Source=pdv.db");
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
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("AdminOrManager", policy =>
                policy.RequireRole("Admin", "Manager"));
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
        builder.Services.AddScoped<IFiscalIssuer, FiscalDocumentService>();
        builder.Services.AddScoped<SaleService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<SupplierService>();
        builder.Services.AddScoped<PurchaseEntryService>();
        builder.Services.AddScoped<LoyaltyService>();
        builder.Services.AddScoped<CommissionService>();
        builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
        builder.Services.AddSingleton<IReceiptPrinter, VirtualMockReceiptPrinter>();
        builder.Services.AddSingleton<IScaleService, VirtualMockScaleService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        else
        {
            app.UseExceptionHandler();
            if (httpsRedirection)
            {
                app.UseHsts();
            }
        }

        if (httpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            ctx.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; connect-src 'self'";
            await next();
        });

        if (serveStaticFiles)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

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
        app.MapAuditLogEndpoints();
        app.MapLoyaltyEndpoints();
        app.MapCommissionEndpoints();
        app.MapPaymentEndpoints();

        if (serveStaticFiles)
        {
            // Client-side routes (/pdv, /catalog, …) are not server routes; anything unmatched
            // that is not an API path returns the SPA shell so a deep link still loads.
            app.MapFallbackToFile("index.html");
        }

        return app;
    }
}
