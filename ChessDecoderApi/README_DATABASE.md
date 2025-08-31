# Chess Decoder API - Database Setup

This project uses PostgreSQL with Entity Framework Core for data persistence.

## 🚀 Quick Start

### 1. Install Dependencies
```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Restore packages
dotnet restore
```

### 2. Local Development Setup
```bash
# Run the automated setup script
./scripts/setup-local-db.sh

# Or manually:
# 1. Install PostgreSQL
# 2. Create database and user
# 3. Update appsettings.json
# 4. Run migrations
```

### 3. Run Migrations
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Test Database
```bash
dotnet run
# Visit: https://localhost:5001/api/test/health
```

## ☁️ Google Cloud Setup

```bash
# Run the automated setup script
./scripts/setup-google-cloud.sh

# Or manually follow DATABASE_SETUP.md
```

## 📊 Database Schema

### Users Table
- **Id**: Primary key (Google Auth ID)
- **Email**: Unique email address
- **Name**: User's display name
- **Credits**: Available processing credits
- **Provider**: Authentication provider
- **CreatedAt/LastLoginAt**: Timestamps

### ChessGames Table
- **Id**: Unique game identifier
- **UserId**: Foreign key to Users
- **Title/Description**: Game metadata
- **PgnContent**: Chess notation content
- **PgnOutputPath**: Generated PGN file path
- **ProcessingTimeMs**: Performance metric
- **IsValid**: Validation status

### GameImages Table
- **Id**: Unique image identifier
- **ChessGameId**: Foreign key to ChessGames
- **FileName/FilePath**: Image storage info
- **FileSizeBytes**: File size
- **FileType**: Image format

### GameStatistics Table
- **Id**: Unique statistics identifier
- **ChessGameId**: Foreign key to ChessGames
- **TotalMoves/ValidMoves/InvalidMoves**: Move counts
- **Opening**: Chess opening name
- **Result**: Game result

## 🔧 Configuration

### Connection Strings
- **Local**: `Host=localhost;Database=chessdecoder;Username=user;Password=pass`
- **Google Cloud**: `Host=/cloudsql/PROJECT:REGION:INSTANCE;Database=chessdecoder;Username=user;Password=pass`

### Environment Variables
```env
DB_HOST=localhost
DB_NAME=chessdecoder
DB_USER=username
DB_PASSWORD=password
ASPNETCORE_ENVIRONMENT=Development
```

## 📝 API Endpoints

### Test Endpoints
- `GET /api/test/health` - Database health check
- `GET /api/test/users` - List all users
- `GET /api/test/users/{id}/credits` - Get user credits
- `POST /api/test/users/{id}/credits/add` - Add credits
- `GET /api/test/games` - List all games
- `GET /api/test/stats` - Database statistics

## 🛠️ Development

### Adding New Migrations
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Seeding Data
Edit `Data/ChessDecoderDbContext.cs` → `SeedData()` method

### Database First Approach
```bash
# Generate models from existing database
dotnet ef dbcontext scaffold "ConnectionString" Npgsql.EntityFrameworkCore.PostgreSQL -o Models -c ChessDecoderDbContext
```

## 🔒 Security

- Use strong passwords for production
- Never commit credentials to version control
- Use environment variables for sensitive data
- Enable SSL for production connections
- Regular database backups

## 📚 Documentation

- [DATABASE_SETUP.md](DATABASE_SETUP.md) - Detailed setup guide
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)

## 🆘 Troubleshooting

### Common Issues
1. **Connection refused**: Check PostgreSQL service
2. **Authentication failed**: Verify credentials
3. **Migration errors**: Check user permissions
4. **Google Cloud**: Verify instance name format

### Useful Commands
```bash
# Check PostgreSQL status
brew services list | grep postgresql  # macOS
sudo systemctl status postgresql      # Linux

# Connect to database
psql -h localhost -U username -d chessdecoder

# List tables
\dt

# Check migrations
dotnet ef migrations list
```
