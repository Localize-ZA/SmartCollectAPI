# SmartCollect API Development Setup

## Quick Start

1. **Start Infrastructure:**
   ```powershell
   docker-compose -f docker-compose.dev.yml up -d
   ```

2. **Verify Services:**
   - PostgreSQL: localhost:5432 (user: smartcollect_user, password: smartcollect_password)
   - Redis: localhost:6379
   - pgAdmin: http://localhost:8080 (admin@smartcollect.local / admin123)
   - Redis Commander: http://localhost:8081

3. **Run API:**
   ```powershell
   cd Server
   dotnet run
   ```

## Environment Configuration

Create `appsettings.Development.json` with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=smartcollect;Username=smartcollect_user;Password=smartcollect_password",
    "Redis": "localhost:6379"
  },
  "GoogleCloud": {
    "ProjectId": "your-gcp-project-id",
    "CredentialsPath": "path/to/service-account.json"
  },
  "Gmail": {
    "ClientId": "your-gmail-client-id",
    "ClientSecret": "your-gmail-client-secret"
  }
}
```

## Stop Infrastructure

```powershell
docker-compose -f docker-compose.dev.yml down
```

## Clean Reset

```powershell
docker-compose -f docker-compose.dev.yml down -v
docker-compose -f docker-compose.dev.yml up -d
```