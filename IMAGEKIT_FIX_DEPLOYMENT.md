# ImageKit Upload Fix - Deployment Steps

## Changes Made

1. ✅ Updated `appsettings.json` with ImageKit credentials
2. ✅ Updated `appsettings.Production.json` with ImageKit credentials  
3. ✅ Added better error handling in `ImageKitFileStorageService.cs`
4. ✅ Enhanced error logging in `ExceptionHandlingMiddleware.cs`

## ImageKit Configuration

```json
"ImageKit": {
  "PublicKey": "public_1bswA0Vq66mBJQlYJxBAyPJm3dE=",
  "PrivateKey": "private_I+B7d2/bfoZkFllZCf07835bjb8=",
  "UrlEndpoint": "https://ik.imagekit.io/fnyx4x87z"
}
```

## Deployment Steps

### 1. Build the Project
```cmd
cd Zadana-Backend
dotnet clean
dotnet build -c Release
```

### 2. Publish the Application
```cmd
dotnet publish src/Zadana.Api/Zadana.Api.csproj -c Release -o ./publish
```

### 3. Deploy to runasp.net
- Upload the contents of `./publish` folder to your runasp.net hosting
- Ensure `appsettings.Production.json` is included
- Restart the application

### 4. Verify the Fix
- Try uploading a file from the frontend
- Check the server logs if it still fails
- The error message should now be more descriptive

## Troubleshooting

If upload still fails after deployment:

1. **Check Server Logs**: Look for the detailed error message we added
2. **Verify Configuration**: Ensure appsettings.Production.json is deployed
3. **Test ImageKit Credentials**: Try uploading directly via ImageKit dashboard
4. **Check File Size**: Ensure the file isn't too large (ImageKit free tier has limits)
5. **Verify Network**: Ensure runasp.net can reach ImageKit API

## Alternative: Use Local Storage (Development Only)

If you want to test without ImageKit, change Program.cs:

```csharp
// Replace this line:
builder.Services.AddTransient<IFileStorageService, ImageKitFileStorageService>();

// With this:
builder.Services.AddTransient<IFileStorageService, LocalFileStorageService>();
```

Note: Local storage won't work on runasp.net as it's a shared hosting environment.
