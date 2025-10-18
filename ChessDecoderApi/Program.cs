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
    // Try multiple ways to get the connection string
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    
    Console.WriteLine($"[Database] Configuration connection string: '{connectionString}'");
    Console.WriteLine($"[Database] Environment connection string: '{envConnectionString}'");
    
    // Use environment variable if available, otherwise use configuration
    var finalConnectionString = !string.IsNullOrEmpty(envConnectionString) ? envConnectionString : connectionString;
    
    if (string.IsNullOrEmpty(finalConnectionString))
    {
        // Default to SQLite for cost-effective deployment
        var dbPath = Path.Combine("/app", "data", "chessdecoder.db");
        var dbDir = Path.GetDirectoryName(dbPath);
        
        Console.WriteLine($"[Database] No connection string found, using default path: {dbPath}");
        
        try
        {
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir!);
                Console.WriteLine($"[Database] Created directory: {dbDir}");
            }
            
            // Verify directory was created and is writable
            if (Directory.Exists(dbDir))
            {
                Console.WriteLine($"[Database] Directory exists and is accessible: {dbDir}");
            }
            else
            {
                throw new DirectoryNotFoundException($"Failed to create directory: {dbDir}");
            }
            
            options.UseSqlite($"Data Source={dbPath}");
            Console.WriteLine($"[Database] Using SQLite database at: {dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Error setting up database path: {ex.Message}");
            Console.WriteLine($"[Database] Stack trace: {ex.StackTrace}");
            
            // Fallback to current directory
            var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "chessdecoder.db");
            options.UseSqlite($"Data Source={fallbackPath}");
            Console.WriteLine($"[Database] Using fallback SQLite database at: {fallbackPath}");
        }
    }
    else
    {
        // Use the provided connection string
        options.UseSqlite(finalConnectionString);
        Console.WriteLine($"[Database] Using provided connection string: {finalConnectionString}");
    }
});

// Register services
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IChessMoveProcessor, ChessMoveProcessor>();
builder.Services.AddScoped<IChessMoveValidator, ChessMoveValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<ICloudStorageService, CloudStorageService>();

// Register Firestore service (FREE database - no cost for typical usage!)
builder.Services.AddSingleton<IFirestoreService, FirestoreService>();

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

// Configure the port - only override for Cloud Run (production)
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    Console.WriteLine("========================================");
    Console.WriteLine("[Database] Initializing database...");
    
    // Check if Firestore is available (preferred - FREE option!)
    var firestoreService = scope.ServiceProvider.GetRequiredService<IFirestoreService>();
    var isFirestoreAvailable = await firestoreService.IsAvailableAsync();
    
    if (isFirestoreAvailable)
    {
        Console.WriteLine("[Database] ✅ Using Firestore (NoSQL)");
        Console.WriteLine("[Database] 💰 Cost: FREE for typical usage!");
        Console.WriteLine("[Database]    - 1 GB storage");
        Console.WriteLine("[Database]    - 50,000 reads/day");
        Console.WriteLine("[Database]    - 20,000 writes/day");
        Console.WriteLine("[Database] Firestore is ready - no migrations needed");
        Console.WriteLine("========================================");
    }
    else
    {
        Console.WriteLine("[Database] ⚠️  Firestore not available");
        Console.WriteLine("[Database] Using SQLite as fallback (for development only)");
        Console.WriteLine("[Database] To enable Firestore:");
        Console.WriteLine("[Database]   1. gcloud services enable firestore.googleapis.com");
        Console.WriteLine("[Database]   2. gcloud firestore databases create --location=us-central --type=firestore-native");
        Console.WriteLine("[Database]   3. Set GOOGLE_CLOUD_PROJECT environment variable");
        Console.WriteLine("========================================");
        
        // Fallback to SQLite for local development
        var context = scope.ServiceProvider.GetRequiredService<ChessDecoderDbContext>();
        
        try
        {
            Console.WriteLine("[Database] Initializing SQLite...");
            
            var canConnect = await context.Database.CanConnectAsync();
            Console.WriteLine($"[Database] SQLite connection test: {canConnect}");
            
            if (!canConnect)
            {
                Console.WriteLine("[Database] Creating SQLite database...");
                await context.Database.EnsureCreatedAsync();
                Console.WriteLine("[Database] SQLite database created");
            }
            else
            {
                var pendingMigrations = context.Database.GetPendingMigrations().ToList();
                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"[Database] Applying {pendingMigrations.Count} migrations...");
                    await context.Database.MigrateAsync();
                    Console.WriteLine("[Database] Migrations applied");
                }
            }
            
            Console.WriteLine("[Database] SQLite initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Error initializing SQLite: {ex.Message}");
            throw;
        }
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
