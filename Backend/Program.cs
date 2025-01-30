using Backend.Application.Configuration;
using Backend.Application.Interfaces.EmailServices;
using Backend.Application.Interfaces.JWT;
using Backend.Application.Interfaces.RecaptchaService;
using Backend.Application.Interfaces.UserServices;
using Backend.Application.Services.EmailServices;
using Backend.Application.Services.JWT;
using Backend.Application.Services.UserServices;
using Backend.Application.Services.Validation;
using Backend.Application.Settings;
using Backend.Application.Validators;
using Backend.Domain.Entities;
using Backend.Infrastructure.Data.Sql.Interfaces;
using Backend.Infrastructure.Data.Sql.Provider;
using Backend.Infrastructure.Data.Sql.UserQueries;
using Backend.Infrastructure.Email;
using Backend.Infrastructure.Helpers;
using Backend.Infrastructure.Helpers.Converters;
using Backend.Infrastructure.Interfaces;
using Backend.Infrastructure.Providers;
using Backend.Infrastructure.Security;
using Backend.Infrastructure.WebSockets;
using Backend.Tests.Mocks;
using Dapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MySqlConnector;
using Serilog;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Backend.Application.Services.AuthService;
using Backend.Application.Interfaces.AuthService;

var builder = WebApplication.CreateBuilder(args);

#region Configuration and Services
// Register In-Memory Distributed Cache
builder.Services.AddDistributedMemoryCache();
// Register Redis Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis"); // Ensure this is set in appsettings.json or environment variables
    options.InstanceName = "YourAppInstanceName"; // Optional: prefix for keys
});
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Set up JWT settings
var jwtSettings = new JwtSettings
{
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "eBudget",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "eBudget",
    SecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "development-fallback-key",
    ExpiryMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES"), out var expiry) ? expiry : 60
};
#endregion

#region Serilog Configuration
// Configure Serilog early in the application lifecycle
var logFilePath = builder.Environment.IsProduction()
    ? "/var/www/backend/logs/app-log.txt"
    : "logs/app-log.txt";  // Default to this path for test and dev environments

// Initialize Serilog and set up logging before anything else
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
    /*
    .WriteTo.Graylog(new GraylogSinkOptions
    {
        HostnameOrAddress = "192.168.50.61",
        Port = 12201
    })
    */
    .CreateLogger();
// Log the file path after Serilog is initialized
Log.Information($"Log file path: {logFilePath}");

// Set Serilog as the logging provider
builder.Host.UseSerilog();
#endregion

#region Application Build Information
// Get the build date and time of the application
var buildDateTime = BuildInfoHelper.GetBuildDate(Assembly.GetExecutingAssembly());
Log.Information($"Application build date and time: {buildDateTime}");
#endregion

#region Dapper Type Handler
SqlMapper.AddTypeHandler(new GuidTypeHandler());
#endregion

// Continue configuring services and the rest of the application
var configuration = builder.Configuration;

// Add services to the container
#region Injected Services

// Section for SQL related services
builder.Services.AddScoped<IUserSqlExecutor, UserSqlExecutor>();
builder.Services.AddScoped<ITokenSqlExecutor, TokenSqlExecutor>();
builder.Services.AddScoped<IAuthenticationSqlExecutor, AuthenticationSqlExecutor>();
// Add the UserSQLProvider to the services
builder.Services.AddScoped<IUserSQLProvider, UserSQLProvider>();

// Section for user services
builder.Services.AddScoped<IUserServices, UserServices>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IUserTokenService, UserTokenService>();
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();

// Section for email services
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserEmailService, UserEmailService>();
builder.Services.AddScoped<IEmailPreparationService, EmailPreparationService>();
builder.Services.AddScoped<IEmailResetPasswordService, EmailResetPasswordService>();

// Other various services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<LogHelper>();
builder.Services.AddScoped<ITimeProvider, SystemTimeProvider>();
// Remove CookieService if not using cookies
// builder.Services.AddScoped<ICookieService, CookieService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IEnvironmentService, EnvironmentService>();
builder.Services.AddTransient<RecaptchaHelper>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();
builder.Services.Configure<ResendEmailSettings>(builder.Configuration.GetSection("ResendEmailSettings"));
builder.Services.AddScoped<DbConnection>(provider =>
{
    var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string not found in environment variables.");
    }

    // Return a new MySqlConnection instance with the connection string
    return new MySqlConnection(connectionString);
});

// Configure EmailService based on environment
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailService, MockEmailService>();

    var mockEmailPreparationService = new Mock<IEmailPreparationService>();
    mockEmailPreparationService
        .Setup(service => service.PrepareVerificationEmailAsync(It.IsAny<EmailMessageModel>()))
        .ReturnsAsync((EmailMessageModel email) => email);

    mockEmailPreparationService
        .Setup(service => service.PrepareContactUsEmailAsync(It.IsAny<EmailMessageModel>()))
        .ReturnsAsync((EmailMessageModel email) => email);

    builder.Services.AddSingleton<IEmailPreparationService>(mockEmailPreparationService.Object);
}
else
{
    builder.Services.AddSingleton<IEmailService, EmailService>();
    builder.Services.AddSingleton<IEmailPreparationService, EmailPreparationService>();
}

// Add WebSockets and their helpers
builder.Services.AddSingleton<IWebSocketManager, AuthWebSocketManager>();
builder.Services.AddHostedService(provider => (AuthWebSocketManager)provider.GetRequiredService<IWebSocketManager>());

#endregion

#region Rate Limiter Configuration
// Add rate limiting services
builder.Services.AddRateLimiter(options =>
{
    // Set a global limiter using PartitionedRateLimiter.Create
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,               // Allow 10 requests per minute
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // Registration-specific rate limit policy
    options.AddPolicy("RegistrationPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress!, key =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,                 // Allow 3 requests
                Window = TimeSpan.FromMinutes(2), // In a 2-minute window
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0                   // No queueing
            }));
    // Login rate limit policy
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress!, key =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,                  // Allow 5 login attempts
                Window = TimeSpan.FromMinutes(15), // Within a 15-minute window
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0                    // No queuing for additional requests
            }));
    // Email sending rate limit policy
    // is used in the EmailController
    // Todo: Measure and adjust the rate limit for production
    options.AddPolicy("EmailSendingPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress!, key =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Log rate-limiting rejections
    options.OnRejected = (context, cancellationToken) =>
    {
        var policyName = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "Global";
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Rate limit exceeded: Policy={Policy}, IP={IP}, Endpoint={Endpoint}",
            policyName, ipAddress, context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return new ValueTask(context.HttpContext.Response.WriteAsync(
            "Rate limit exceeded. Please try again later.", cancellationToken));
    };

});
#endregion

#region Services and Middleware Registration

// Add controllers to support routing
builder.Services.AddControllers();

// Add Swagger services
builder.Services.AddSwaggerGen();

// Add FluentValidation for DTO validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<UserValidator>();

// Add CORS policies
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Local frontend URL
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Remove if not needed
    });

    options.AddPolicy("ProductionCorsPolicy", policy =>
    {
        policy.WithOrigins("https://ebudget.se", "https://www.ebudget.se") // Production URLs
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Remove if not needed
    });

    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// *** Configure JWT Bearer Authentication Only ***
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Disable the default claim type mapping
    options.MapInboundClaims = false;

    if (string.IsNullOrEmpty(jwtSettings.SecretKey) || jwtSettings.SecretKey == "development-fallback-key")
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogWarning("JWT_SECRET_KEY is missing or using fallback. Update your environment configuration.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer = true,  // Enable if you want to validate issuer
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true, // Enable if you want to validate audience
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
    };

    // Configure JWT bearer events
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("OnMessageReceived fired.");

            // *** Remove JWT extraction from cookies ***
            // Pure header-based JWT authentication
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var claims = context.Principal?.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            logger.LogInformation("Token validated. Claims: {Claims}", string.Join(", ", claims));
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Remove redundant logging configurations
// *** Remove temporary logging setup ***
/* 
//Temp
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});
*/

// Remove duplicate logging configurations if any
// Ensure logging is configured only once, preferably with Serilog as above

// Add Health Checks
builder.Services.AddHealthChecks();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// *** Remove redundant logging configurations ***
/* 
// Configure Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
*/

#endregion

#region Application Pipeline Configuration

// Build the app after service registration
var app = builder.Build();

// Apply CORS based on the environment
if (builder.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCorsPolicy");
}
else if (builder.Environment.IsProduction())
{
    app.UseCors("ProductionCorsPolicy");
}
else
{
    app.UseCors("DefaultCorsPolicy"); // Fallback for other environments
}

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        logger.LogError(exception, "An unhandled exception occurred.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("An unexpected error occurred.");
    });
});

// Middleware for static files with caching headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
    }
});

// Enable routing
app.UseRouting();

// Enable Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Use Rate Limiting Middleware (if configured)
app.UseRateLimiter();

// Swagger configuration
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = "api-docs";
        c.DisplayRequestDuration();
        c.DefaultModelsExpandDepth(-1); // Collapse models by default
    });
}

// HTTPS redirection
app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Map health checks
app.MapHealthChecks("/health");

// Fallback to React's index.html for SPA routing
app.MapFallbackToFile("index.html");

// *** WebSockets Middleware Adjustment ***
app.UseWebSockets();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.Map("/ws/auth", async context =>
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketManager>();
            await webSocketManager.HandleConnectionAsync(webSocket, context);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    });
});

#endregion

#region Logging and Final Setup

// Get the logger after app is built
ILogger<Program> _logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log environment information
var environment = builder.Environment.EnvironmentName;
_logger.LogInformation("Environment: {Environment}", environment);
_logger.LogInformation("Application setup complete. Running app...");

// Run the application
app.Run();

#endregion

// Declare the partial Program class
public partial class Program { }
