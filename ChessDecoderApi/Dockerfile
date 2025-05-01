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

# Expose the port your application uses
EXPOSE 80
EXPOSE 443
EXPOSE 5000

# Add these before the ENTRYPOINT
ENV ASPNETCORE_URLS=http://+:80;http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development

# Set the entry point for the application
ENTRYPOINT ["dotnet", "ChessDecoderApi.dll"] 