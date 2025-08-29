using ChessDecoderApi.Services;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

// Load environment variables from .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IChessMoveProcessor, ChessMoveProcessor>();
builder.Services.AddScoped<IChessMoveValidator, ChessMoveValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
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
            var originsToUse = allowedOrigins ?? new[] { "https://chess-scribe-convert.lovable.app", "https://62ad5c43-6c34-4327-a33d-f77c21343ea5.lovableproject.com", "http://localhost:8080", "http://localhost:5100" };

            Console.WriteLine($"[CORS Policy] Production environment: Restricting to origins: {string.Join(", ", originsToUse)}");
            
            policy.WithOrigins(originsToUse)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

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
