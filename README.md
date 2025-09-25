# SmartCollectAPI

Dev quick start (Hour 1-3):

1. Start infra (Redis + Postgres):

```powershell
docker compose -f .\docker-compose.dev.yml up -d
```

2. Run API (Development):

```powershell
cd .\Server
dotnet run
```

3. Client dashboard (optional):

```powershell
cd .\client
npm install
npm run dev
```

4. Tests (Redis/Postgres integration are skipped if services are offline):

```powershell
dotnet test .\Server.Tests\Server.Tests.csproj
```
