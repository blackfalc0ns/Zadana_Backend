# 🚀 Deploy to runasp.net - READY TO GO

## Current Status
✅ Backend code has been fixed with ImageKit configuration  
✅ Enhanced error handling and logging added  
✅ Project built and published to `Zadana-Backend/publish/` folder  
❌ **NOT YET DEPLOYED** - Live server still has old code

## What's Fixed
1. ImageKit credentials added to `appsettings.Production.json`
2. Better error messages in `ImageKitFileStorageService.cs`
3. Detailed error logging in `ExceptionHandlingMiddleware.cs`

## Deployment Steps for runasp.net

### Step 1: Access Your runasp.net Control Panel
1. Go to https://runasp.net
2. Login to your account
3. Navigate to your application: **zadana.runasp.net**

### Step 2: Stop the Application
- Find the "Stop" or "Restart" button in the control panel
- Stop the application before uploading new files

### Step 3: Upload Published Files
You need to upload ALL files from: `D:\fullstack project\Zadana\Zadana-Backend\publish\`

**Important files to verify are uploaded:**
- ✅ `Zadana.Api.dll` (main application)
- ✅ `appsettings.Production.json` (contains ImageKit config)
- ✅ All other DLL files
- ✅ `web.config` (IIS configuration)

**Upload Methods:**
- **FTP**: Use FileZilla or similar FTP client
- **Control Panel File Manager**: Upload via web interface
- **ZIP Upload**: Zip the publish folder and upload (if supported)

### Step 4: Verify Configuration File
After upload, verify `appsettings.Production.json` contains:

```json
"ImageKit": {
  "PublicKey": "public_1bswA0Vq66mBJQlYJxBAyPJm3dE=",
  "PrivateKey": "private_I+B7d2/bfoZkFllZCf07835bjb8=",
  "UrlEndpoint": "https://ik.imagekit.io/fnyx4x87z"
}
```

### Step 5: Start the Application
- Click "Start" or "Restart" in the control panel
- Wait 30-60 seconds for the application to fully start

### Step 6: Test the Fix

#### Test 1: Health Check
Open in browser: `https://zadana.runasp.net/health`

Expected response:
```json
{
  "status": "Healthy",
  "database": "Connected",
  "timestamp": "2026-03-16T..."
}
```

#### Test 2: File Upload
1. Open your frontend application
2. Navigate to Master Products
3. Try uploading an image
4. **If it works**: ✅ Success!
5. **If it fails**: Check the error message (should now be detailed)

### Step 7: Check Logs (If Still Failing)
If upload still fails, the error message should now show:
- Specific ImageKit error details
- Configuration issues
- Network problems

Look for logs in runasp.net control panel under:
- Application Logs
- Error Logs
- stdout logs (if available)

## Troubleshooting

### Issue: Still getting 500 error after deployment

**Check:**
1. Did you upload `appsettings.Production.json`?
2. Did you restart the application?
3. Check the error message - it should now be detailed

**Common causes:**
- Old files cached - try clearing browser cache
- Application not restarted properly
- Configuration file not uploaded

### Issue: ImageKit authentication error

**Verify:**
- PublicKey starts with `public_`
- PrivateKey starts with `private_`
- UrlEndpoint is exactly: `https://ik.imagekit.io/fnyx4x87z`
- No extra spaces or quotes in the values

### Issue: Can't access runasp.net control panel

**Contact runasp.net support:**
- Email: support@runasp.net
- Or use their support ticket system

## Alternative: Deploy via FTP

If you have FTP credentials:

```
Host: ftp.runasp.net (or your specific FTP host)
Username: [your username]
Password: [your password]
Port: 21 (or 22 for SFTP)
```

**Steps:**
1. Connect via FileZilla
2. Navigate to your application folder (usually `/site/wwwroot`)
3. Delete old files (backup first!)
4. Upload all files from `Zadana-Backend/publish/`
5. Restart application via control panel

## Quick Reference

**Published files location:**
```
D:\fullstack project\Zadana\Zadana-Backend\publish\
```

**Live server:**
```
https://zadana.runasp.net
```

**Test endpoints after deployment:**
- Health: `https://zadana.runasp.net/health`
- Swagger: `https://zadana.runasp.net/swagger`
- Upload: `POST https://zadana.runasp.net/api/files/upload`

## Need Help?

If you're unsure how to upload files to runasp.net:
1. Check runasp.net documentation
2. Look for "File Manager" or "FTP" in control panel
3. Contact their support for deployment instructions

---

**Remember:** The fix is ready, you just need to deploy it! 🚀
