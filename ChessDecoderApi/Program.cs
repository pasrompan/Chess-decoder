using ChessDecoderApi.Services;
using ChessDecoderApi.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Load environment variables from .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework Core with SQLite support
builder.Services.AddDbContext<ChessDecoderDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        // Default to SQLite for cost-effective deployment
        var dbPath = Path.Combine("/app", "data", "chessdecoder.db");
        var dbDir = Path.GetDirectoryName(dbPath);
        
        try
        {
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir!);
                Console.WriteLine($"[Database] Created directory: {dbDir}");
            }
            
            options.UseSqlite($"Data Source={dbPath}");
            Console.WriteLine($"[Database] Using SQLite database at: {dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Error setting up database path: {ex.Message}");
            // Fallback to current directory
            var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "chessdecoder.db");
            options.UseSqlite($"Data Source={fallbackPath}");
            Console.WriteLine($"[Database] Using fallback SQLite database at: {fallbackPath}");
        }
    }
    else
    {
        // SQLite connection
        options.UseSqlite(connectionString);
        Console.WriteLine("[Database] Using SQLite database");
    }
});

// Register services
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IChessMoveProcessor, ChessMoveProcessor>();
builder.Services.AddScoped<IChessMoveValidator, ChessMoveValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<ICloudStorageService, CloudStorageService>();
builder.Services.AddHttpClient();

// Load environment variables - includes both .env and system environment variables
builder.Configuration.AddEnvironmentVariables();

builder.Logging.ClearProviders().AddConsole().SetMinimumLevel(LogLevel.Information);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        // Log the current environment for CORS configuration
        Console.WriteLine($"[CORS Policy] Configuring for environment: '{builder.Environment.EnvironmentName}'");

        if (builder.Environment.IsDevelopment())
        {
            // More permissive for development
            Console.WriteLine("[CORS Policy] Development environment: Allowing any origin, method, and header.");
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Restrictive for production
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
            
            // Also check environment variable for CORS origins
            var envOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
            if (!string.IsNullOrEmpty(envOrigins))
            {
                allowedOrigins = envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim()).ToArray();
            }
            
            var originsToUse = allowedOrigins ?? new[] { "https://chess-scribe-convert.lovable.app" };

            Console.WriteLine($"[CORS Policy] Production environment: Restricting to origins: {string.Join(", ", originsToUse)}");
            
            policy.WithOrigins(originsToUse)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Configure the port for Cloud Run
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ChessDecoderDbContext>();
    var cloudStorage = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
    
    try
    {
        Console.WriteLine("[Database] Starting database initialization...");
        
        // Test database connection first
        Console.WriteLine("[Database] Testing database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"[Database] Database connection test result: {canConnect}");
        
        // Try to sync database from cloud first (in production)
        if (!app.Environment.IsDevelopment())
        {
            Console.WriteLine("[Database] Attempting to sync database from Cloud Storage...");
            try
            {
                var synced = await cloudStorage.SyncDatabaseFromCloudAsync();
                if (synced)
                {
                    Console.WriteLine("[Database] Database synced from Cloud Storage successfully");
                }
                else
                {
                    Console.WriteLine("[Database] No cloud database found, will create a new local database");
                }
            }
            catch (Exception syncEx)
            {
                Console.WriteLine($"[Database] Cloud sync failed (non-critical): {syncEx.Message}");
            }
        }
        
        // Ensure database is created
        Console.WriteLine("[Database] Ensuring database is created...");
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("[Database] Database created/verified successfully");
        
        // Apply any pending migrations
        var pendingMigrations = context.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            Console.WriteLine($"[Database] Applying {pendingMigrations.Count} pending migrations...");
            await context.Database.MigrateAsync();
            Console.WriteLine("[Database] Migrations applied successfully");
        }
        else
        {
            Console.WriteLine("[Database] No pending migrations");
        }
        
        // Sync database to cloud after initialization (in production)
        if (!app.Environment.IsDevelopment())
        {
            Console.WriteLine("[Database] Syncing database to Cloud Storage...");
            try
            {
                var cloudSynced = await cloudStorage.SyncDatabaseToCloudAsync();
                if (cloudSynced)
                {
                    Console.WriteLine("[Database] Database synced to Cloud Storage successfully");
                }
                else
                {
                    Console.WriteLine("[Database] Failed to sync database to Cloud Storage - continuing with local database");
                }
            }
            catch (Exception syncEx)
            {
                Console.WriteLine($"[Database] Cloud sync failed (non-critical): {syncEx.Message}");
            }
        }
        
        Console.WriteLine("[Database] Database initialization completed successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database] Critical error initializing database: {ex.Message}");
        Console.WriteLine($"[Database] Stack trace: {ex.StackTrace}");
        throw;
    }
}

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS must be applied before Authorization
app.UseCors("CorsPolicy");

app.UseAuthorization();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        
        var response = new
        {
            status = context.Response.StatusCode,
            message = "An internal server error occurred.",
            detail = app.Environment.IsDevelopment() ? exception?.Message : null
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    });
});

app.MapControllers();

app.Run();
