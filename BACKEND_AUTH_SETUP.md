# Backend Authentication Setup Guide

This guide explains how to set up and run the backend authentication system for ChessScribe.

## 🚀 **What's Been Added**

### **1. Authentication Models**
- `User.cs` - User data model
- `AuthRequest.cs` - Authentication request model
- `AuthResponse.cs` - Authentication response model
- `UserProfileRequest.cs` - Profile update request model
- `UserProfileResponse.cs` - Profile update response model

### **2. Authentication Service**
- `IAuthService.cs` - Authentication service interface
- `AuthService.cs` - Implementation of Google OAuth verification

### **3. Authentication Controller**
- `AuthController.cs` - REST API endpoints for authentication

### **4. Dependencies Added**
- `Google.Apis.Auth` - Google OAuth token verification
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication support
- `System.IdentityModel.Tokens.Jwt` - JWT token handling

## 🔧 **Setup Instructions**

### **Step 1: Install Dependencies**
```bash
cd ChessDecoderApi
dotnet restore
```

### **Step 2: Create Environment File**
Create a `.env` file in the root directory with:
```env
# Google OAuth Configuration
GOOGLE_CLIENT_ID=1068929110698-j5u8k8u78v791cs39qc25gvkqskhng82.apps.googleusercontent.com

# Backend Configuration
ASPNETCORE_URLS=http://localhost:5100
ASPNETCORE_ENVIRONMENT=Development

# CORS Origins (comma-separated)
AllowedOrigins=http://localhost:8080,http://localhost:5100,https://chess-scribe-convert.lovable.app
```

### **Step 3: Run the Backend**
```bash
cd ChessDecoderApi
dotnet run
```

The API will start on `http://localhost:5100`

## 📡 **API Endpoints**

### **Authentication**
- `POST /api/auth/verify` - Verify Google OAuth token
- `GET /api/auth/profile?userId={id}` - Get user profile
- `PUT /api/auth/profile?userId={id}` - Update user profile
- `POST /api/auth/signout?userId={id}` - Sign out user

### **Existing Endpoints**
- Your existing chess decoding endpoints remain unchanged

## 🧪 **Testing the Authentication**

### **1. Start Backend**
```bash
cd ChessDecoderApi
dotnet run
```

### **2. Test with Frontend**
1. Start your frontend: `npm run dev`
2. Go to `http://localhost:8080/signin`
3. Click "Continue with Google"
4. Complete Google authentication
5. Check backend logs for successful verification

### **3. Test API Directly**
```bash
# Verify token (replace with actual access token)
curl -X POST http://localhost:5100/api/auth/verify \
  -H "Content-Type: application/json" \
  -d '{"accessToken": "your_google_access_token_here"}'
```

## 🔍 **How It Works**

### **1. Frontend Flow**
1. User clicks "Continue with Google"
2. Google OAuth popup opens
3. User authenticates with Google
4. Frontend receives access token
5. Frontend sends token to backend for verification

### **2. Backend Flow**
1. Receives access token from frontend
2. Calls Google API to get user info
3. Creates/updates user in memory storage
4. Returns user information to frontend
5. Frontend creates user session

### **3. User Management**
- Users are stored in memory (for development)
- In production, you'd use a database
- User profiles can be updated via API
- Sign-out functionality is implemented

## 🚨 **Important Notes**

### **Development vs Production**
- **Current**: In-memory user storage
- **Production**: Should use database (SQL Server, PostgreSQL, etc.)
- **Security**: Add JWT token generation and validation
- **CORS**: Configure for your production domains

### **Google OAuth**
- Uses access tokens (not ID tokens)
- Verifies tokens by calling Google's userinfo endpoint
- Supports profile picture and email verification

### **Error Handling**
- Comprehensive error handling in all endpoints
- Detailed logging for debugging
- Graceful fallbacks for common errors

## 🔮 **Future Enhancements**

### **1. Database Integration**
- Replace in-memory storage with database
- Add user roles and permissions
- Implement user activity tracking

### **2. Security Improvements**
- JWT token generation and validation
- Refresh token support
- Rate limiting and security headers

### **3. Additional Features**
- Microsoft authentication support
- Multi-factor authentication
- User session management

## 🐛 **Troubleshooting**

### **Common Issues**
1. **Port conflicts**: Ensure port 8080 is free
2. **CORS errors**: Check AllowedOrigins in .env
3. **Google API errors**: Verify client ID and OAuth setup
4. **Dependency issues**: Run `dotnet restore`

### **Debug Mode**
- Check backend console logs
- Use Swagger UI at `/swagger`
- Test endpoints with Postman or curl

## 🎯 **Next Steps**

1. **Test the current setup** with your frontend
2. **Add database integration** for production
3. **Implement JWT authentication** for secure API access
4. **Add user management features** as needed

Your backend authentication system is now ready to work with your frontend! 🚀
