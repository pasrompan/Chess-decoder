# Use the official .NET SDK image as the base image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy the project files
COPY . .

# Build the application
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Configure ASP.NET Core to use the environment-provided PORT
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port 8080
EXPOSE 8080

# Set the entry point for the application
ENTRYPOINT ["dotnet", "ChessDecoderApi.dll"] 