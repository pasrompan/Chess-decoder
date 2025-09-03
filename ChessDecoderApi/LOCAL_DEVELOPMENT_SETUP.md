# Local Development Setup Guide

This guide will help you set up the Chess Decoder API for local development, including Google Cloud Storage integration.

## Prerequisites

1. **.NET 9.0 SDK** - Download from [Microsoft's website](https://dotnet.microsoft.com/download)
2. **Google Cloud SDK** (optional, for cloud storage) - Download from [Google Cloud](https://cloud.google.com/sdk/docs/install)

## Quick Start (Local-Only Mode)

If you want to run the application without Google Cloud Storage:

1. **Clone and navigate to the project:**
   ```bash
   cd ChessDecoderApi
   ```

2. **Copy the environment template:**
   ```bash
   cp env.template .env
   ```

3. **Run the application:**
   ```bash
   dotnet run
   ```

The application will run in local-only mode, storing data in a local SQLite database and files in the `uploads/` directory.

## Full Setup (With Google Cloud Storage)

### Step 1: Set up Google Cloud Authentication

You have two options for authentication:

#### Option A: Application Default Credentials (Recommended)

1. **Install Google Cloud SDK** if you haven't already
2. **Authenticate with your Google account:**
   ```bash
   gcloud auth login
   gcloud auth application-default login
   ```
3. **Set your project:**
   ```bash
   gcloud config set project YOUR_PROJECT_ID
   ```

#### Option B: Service Account Key

1. **Create a service account** in Google Cloud Console
2. **Download the JSON key file**
3. **Set the environment variable:**
   ```bash
   export GOOGLE_APPLICATION_CREDENTIALS="/path/to/your/service-account-key.json"
   ```

### Step 2: Configure Environment Variables

1. **Copy the environment template:**
   ```bash
   cp env.template .env
   ```

2. **Edit the `.env` file** with your actual values:
   ```bash
   # Google Cloud Storage Bucket Names
   GOOGLE_CLOUD_DATABASE_BUCKET=your-database-bucket-name
   GOOGLE_CLOUD_IMAGES_BUCKET=your-images-bucket-name
   
   # Other settings...
   ```

### Step 3: Create Google Cloud Storage Buckets

If you don't have buckets yet, create them:

```bash
# Create database bucket
gsutil mb gs://your-database-bucket-name

# Create images bucket
gsutil mb gs://your-images-bucket-name
```

### Step 4: Run the Application

```bash
dotnet run
```

## Environment Variables Reference

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `GOOGLE_APPLICATION_CREDENTIALS` | Path to service account key file | No* | - |
| `GOOGLE_CLOUD_DATABASE_BUCKET` | Database bucket name | No | chessdecoder-db |
| `GOOGLE_CLOUD_IMAGES_BUCKET` | Images bucket name | No | chessdecoder-images |
| `DATABASE_CONNECTION_STRING` | Database connection string | No | Data Source=data/chessdecoder.db |
| `JWT_SECRET_KEY` | JWT signing key | No | - |
| `ASPNETCORE_ENVIRONMENT` | Environment name | No | Development |
| `ALLOWED_ORIGINS` | CORS allowed origins (comma-separated) | No | - |

*Required only if using Google Cloud Storage

## Troubleshooting

### Google Cloud Authentication Error

If you see this error:
```
System.InvalidOperationException: Your default credentials were not found
```

**Solutions:**
1. **Run the authentication command:**
   ```bash
   gcloud auth application-default login
   ```

2. **Or set up a service account key:**
   ```bash
   export GOOGLE_APPLICATION_CREDENTIALS="/path/to/your/key.json"
   ```

3. **Or run in local-only mode** by not setting any Google Cloud credentials

### Database Issues

If you have database issues:
1. **Delete the local database** and let it recreate:
   ```bash
   rm -rf data/chessdecoder.db*
   ```

2. **Run migrations:**
   ```bash
   dotnet ef database update
   ```

### CORS Issues

If you have CORS issues:
1. **Check your `ALLOWED_ORIGINS`** environment variable
2. **In development**, CORS is permissive by default
3. **In production**, make sure your frontend URL is in the allowed origins

## Development vs Production

### Development Mode
- CORS allows any origin
- Google Cloud Storage is optional
- Detailed error messages
- Swagger UI enabled

### Production Mode
- CORS restricted to specific origins
- Google Cloud Storage required
- Generic error messages
- Swagger UI disabled

## File Structure

```
ChessDecoderApi/
├── .env                    # Your local environment variables (ignored by git)
├── env.template           # Template for environment variables
├── appsettings.json       # Base configuration
├── appsettings.Development.json  # Development overrides
├── appsettings.Production.json   # Production overrides (sanitized)
├── data/                  # Local SQLite database
├── uploads/               # Local file uploads
└── ...
```

## Security Notes

- Never commit `.env` files to version control
- The `env.template` file is safe to commit
- Production configuration files are sanitized
- Use strong JWT secrets in production
- Restrict CORS origins in production
