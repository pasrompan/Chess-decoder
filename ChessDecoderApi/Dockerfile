# Multi-stage build for Chess Decoder API
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["ChessDecoderApi.csproj", "./"]
RUN dotnet restore "ChessDecoderApi.csproj"

# Copy source code
COPY . .
RUN dotnet build "ChessDecoderApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ChessDecoderApi.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app

# Install necessary packages for image processing
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create directory for uploaded files
RUN mkdir -p /app/uploads /app/outputs

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "ChessDecoderApi.dll"] 