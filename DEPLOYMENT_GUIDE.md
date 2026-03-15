# Zadana Backend Deployment Guide

## Prerequisites
- .NET 9.0 Runtime installed on server
- SQL Server database accessible from server
- IIS or hosting platform configured

## Deployment Steps

### 1. Build for Production
```bash
cd Zadana-Backend/src/Zadana.Api
dotnet publish -c Release -o ./publish
```

### 2. Configuration Files

#### appsettings.Production.json
Already configured with:
- Database connection string
- JWT settings
- Email settings (Resend)
- SMS settings (Twilio)
- File storage (ImageKit)

**Important**: Update these values before deployment:
- `ConnectionStrings:DefaultConnection` - Your production database
- `JwtSettings:Secret` - Strong secret key (min 32 characters)
- `ResendSettings:ApiKey` - Your Resend API key
- `TwilioSettings` - Your Twilio credentials
- `ImageKit` - Your ImageKit credentials

### 3. Environment Variables (Alternative to appsettings)

You can also set these as environment variables on the server:

```bash
ConnectionStrings__DefaultConnection="Server=...;Database=...;"
JwtSettings__Secret="YourSecretKey"
ResendSettings__ApiKey="re_..."
```

### 4. Database Migration

The app will automatically run migrations on startup. To run manually:

```bash
cd Zadana-Backend/src/Zadana.Infrastructure
dotnet ef database update --project ../Zadana.Api
```

Or use the generated SQL script:
```bash
# Upload and run: Zadana-Backend/src/Zadana.Api/migration.sql
```

### 5. Seed Data

The app will automatically seed initial data on first run:
- Admin account (admin@system.com / Admin@123)
- Roles
- Categories
- Brands
- Units of Measure

To seed manually, run the SQL script:
```bash
# Upload and run: Zadana-Backend/src/Zadana.Api/seed_admin_simple.sql
```

### 6. IIS Deployment

1. **Install ASP.NET Core Hosting Bundle**
   - Download from: https://dotnet.microsoft.com/download/dotnet/9.0
   - Install on server
   - Restart IIS: `iisreset`

2. **Create IIS Site**
   - Open IIS Manager
   - Add new website
   - Point to publish folder
   - Set application pool to "No Managed Code"

3. **Configure Application Pool**
   - .NET CLR Version: No Managed Code
   - Managed Pipeline Mode: Integrated
   - Identity: ApplicationPoolIdentity (or custom account with DB access)

4. **Set Permissions**
   ```powershell
   icacls "C:\path\to\publish" /grant "IIS AppPool\YourAppPoolName:(OI)(CI)F" /T
   ```

5. **Enable Detailed Errors** (for troubleshooting)
   - Edit web.config
   - Set `stdoutLogEnabled="true"`
   - Check logs in `.\logs\stdout` folder

### 7. Azure App Service Deployment

1. **Create App Service**
   - Runtime: .NET 9
   - OS: Windows or Linux

2. **Configure Application Settings**
   - Add all settings from appsettings.Production.json as App Settings
   - Connection strings go in "Connection strings" section

3. **Deploy**
   ```bash
   # Using Azure CLI
   az webapp deployment source config-zip \
     --resource-group YourResourceGroup \
     --name YourAppName \
     --src ./publish.zip
   ```

   Or use Visual Studio / VS Code Azure extension

### 8. Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Zadana.Api/Zadana.Api.csproj", "Zadana.Api/"]
COPY ["src/Zadana.Application/Zadana.Application.csproj", "Zadana.Application/"]
COPY ["src/Zadana.Domain/Zadana.Domain.csproj", "Zadana.Domain/"]
COPY ["src/Zadana.Infrastructure/Zadana.Infrastructure.csproj", "Zadana.Infrastructure/"]
COPY ["src/Zadana.SharedKernel/Zadana.SharedKernel.csproj", "Zadana.SharedKernel/"]
COPY ["src/Zadana.Contracts/Zadana.Contracts.csproj", "Zadana.Contracts/"]
RUN dotnet restore "Zadana.Api/Zadana.Api.csproj"
COPY src/ .
WORKDIR "/src/Zadana.Api"
RUN dotnet build "Zadana.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Zadana.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Zadana.Api.dll"]
```

Build and run:
```bash
docker build -t zadana-api .
docker run -d -p 8080:80 \
  -e ConnectionStrings__DefaultConnection="Server=..." \
  -e JwtSettings__Secret="..." \
  zadana-api
```

## Troubleshooting

### Error 500.30 - App Failed to Start

1. **Check stdout logs**
   - Location: `.\logs\stdout*.log`
   - Enable in web.config: `stdoutLogEnabled="true"`

2. **Common causes:**
   - Missing configuration values (check placeholders like `__SET_...`)
   - Database connection failed
   - Missing .NET runtime
   - File permissions

3. **Enable detailed errors:**
   ```xml
   <httpErrors errorMode="Detailed" />
   ```

### Database Connection Issues

1. **Test connection from server:**
   ```bash
   sqlcmd -S db44714.public.databaseasp.net,1433 -U db44714 -P "password" -d db44714
   ```

2. **Check firewall rules:**
   - Server IP must be whitelisted in database firewall
   - Port 1433 must be open

3. **Connection string format:**
   ```
   Server=db44714.public.databaseasp.net,1433;
   Database=db44714;
   User Id=db44714;
   Password=h!8S5dE#T@t7;
   Encrypt=False;
   TrustServerCertificate=True;
   MultipleActiveResultSets=True;
   Connection Timeout=30;
   ```

### Migrations Not Running

1. **Run manually:**
   ```bash
   dotnet ef database update
   ```

2. **Or use SQL script:**
   - Upload `migration.sql` to database
   - Execute in SQL Server Management Studio or web panel

## Health Check

After deployment, verify:

1. **API is running:**
   ```
   GET https://your-domain.com/health
   ```
   Should return: `{"status":"Healthy","database":"Connected"}`

2. **Swagger UI:**
   ```
   https://your-domain.com/swagger
   ```

3. **Test login:**
   ```
   POST https://your-domain.com/api/identity/login
   {
     "email": "admin@system.com",
     "password": "Admin@123"
   }
   ```

## Security Checklist

- [ ] Change default admin password after first login
- [ ] Update JWT secret to a strong random value
- [ ] Configure HTTPS/SSL certificate
- [ ] Set up CORS properly (don't use AllowAll in production)
- [ ] Enable rate limiting
- [ ] Configure proper logging and monitoring
- [ ] Set up backup for database
- [ ] Change database password (currently exposed in this guide)

## Performance Optimization

1. **Enable response compression**
2. **Configure caching**
3. **Use CDN for static files**
4. **Enable HTTP/2**
5. **Configure connection pooling**
6. **Set up application insights/monitoring**

## Support

For issues:
1. Check stdout logs
2. Check Windows Event Viewer (Application logs)
3. Enable detailed errors temporarily
4. Check database connectivity
5. Verify all configuration values are set
