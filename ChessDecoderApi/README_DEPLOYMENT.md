# Chess Decoder API - Google Cloud Deployment

## 🚀 **Deployment Architecture**

### **Services Used**
- **Cloud Run** - API hosting (serverless)
- **Cloud Storage** - Database sync + image storage
- **Cloud Build** - Automated deployments
- **Secret Manager** - API keys and sensitive data

### **Database Strategy**
- **Local SQLite** - Fast read/write during runtime
- **Cloud Storage Sync** - Periodic backup and sharing
- **Multiple instances** - Can share the same database

### **Image Storage Strategy**
- **Development**: Local file system
- **Production**: Cloud Storage buckets + CDN

## 📋 **Prerequisites**

1. **Google Cloud Project** with billing enabled
2. **gcloud CLI** installed and authenticated
3. **Docker** installed locally (for testing)

## 🛠️ **Setup Steps**

### **1. Initial Setup**
```bash
# Make script executable
chmod +x scripts/setup-google-cloud.sh

# Run automated setup
./scripts/setup-google-cloud.sh
```

### **2. Update Configuration**
Update `appsettings.Production.json` with your bucket names:
```json
{
  "CloudStorage": {
    "DatabaseBucketName": "chessdecoder-db-YOUR_PROJECT_ID",
    "ImagesBucketName": "chessdecoder-images-YOUR_PROJECT_ID"
  }
}
```

### **3. Deploy to Cloud Run**

#### **Option A: Direct Deploy**
```bash
gcloud run deploy chess-decoder-api \
  --source . \
  --region us-central1 \
  --platform managed \
  --allow-unauthenticated \
  --memory 1Gi \
  --cpu 1 \
  --max-instances 10
```

#### **Option B: Cloud Build (Recommended)**
```bash
gcloud builds submit --config cloudbuild.yaml
```

## 🔧 **Configuration**

### **Environment Variables**
```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Data Source=data/chessdecoder.db
```

### **Secrets**
- `openai-api-key` - OpenAI API key
- `google-vision-api-key` - Google Vision API key
- `google-client-id` - Google OAuth client ID
- `gemini-api-key` - Gemini API key (required for OCR)

### **Cloud Storage Buckets**
- **Database Bucket**: `chessdecoder-db-PROJECT_ID`
- **Images Bucket**: `chessdecoder-images-PROJECT_ID`

## 📊 **Cost Estimation**

| Service | Monthly Cost | Notes |
|---------|--------------|-------|
| **Cloud Run** | $2-8 | Based on usage |
| **Cloud Storage** | $0.02-0.05 | Minimal data |
| **Cloud Build** | $0.50-2 | Per build |
| **Secret Manager** | $0.06 | Per secret |
| **Total** | **~$3-11** | Much cheaper than Cloud SQL |

## 🔄 **Database Sync Strategy**

### **On Startup**
1. Download latest database from Cloud Storage
2. Create local SQLite database if none exists
3. Apply any pending migrations

### **Periodic Sync**
- Every 5 minutes: Upload local changes to Cloud Storage
- After critical operations: Immediate sync (optional)

### **Multiple Instances**
- Can share the same database file
- Automatic conflict resolution
- Last-writer-wins strategy

## 📸 **Image Storage Strategy**

### **Development**
- Images stored locally in `./uploads/`
- File paths stored in database

### **Production**
- Images uploaded to Cloud Storage
- URLs stored in database
- CDN for fast delivery
- Automatic cleanup of old images

## 🚀 **Scaling**

### **Auto-scaling**
- Cloud Run scales from 0 to max instances
- Based on HTTP requests
- Cold start: ~2-3 seconds

### **Database Scaling**
- SQLite handles concurrent reads well
- Writes are serialized (acceptable for chess games)
- Multiple instances can share database

## 🔒 **Security**

### **Authentication**
- Google OAuth for user management
- API key validation for external services

### **Data Protection**
- Secrets stored in Secret Manager
- Database synced to private bucket
- Images in public bucket (CDN)

### **Network Security**
- HTTPS enforced
- CORS configured for production domains
- No direct database access

## 📈 **Monitoring**

### **Cloud Run Metrics**
- Request count and latency
- Error rates
- Instance count

### **Application Logs**
- Structured logging to Cloud Logging
- Error tracking and alerting
- Performance monitoring

## 🆘 **Troubleshooting**

### **Common Issues**
1. **Database sync failures**: Check bucket permissions
2. **Image upload errors**: Verify storage permissions
3. **Cold starts**: Normal for serverless, consider min instances
4. **Memory issues**: Increase memory allocation

### **Useful Commands**
```bash
# Check Cloud Run status
gcloud run services describe chess-decoder-api --region us-central1

# View logs
gcloud logging read "resource.type=cloud_run_revision"

# Check bucket contents
gsutil ls gs://chessdecoder-db-PROJECT_ID/
gsutil ls gs://chessdecoder-images-PROJECT_ID/

# Test deployment
curl https://chess-decoder-api-xxx-uc.a.run.app/ChessDecoder/health
```

## 🎯 **Next Steps**

1. **Deploy to Cloud Run**
2. **Test all endpoints**
3. **Monitor performance**
4. **Set up alerts**
5. **Configure custom domain (optional)**
