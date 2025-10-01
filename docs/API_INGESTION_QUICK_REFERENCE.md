# API Ingestion - Quick Reference

## 🎯 All Endpoints

### Source Management
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/sources` | List all sources (supports `?apiType=`, `?enabled=`, `?page=`, `?pageSize=`) |
| GET | `/api/sources/{id}` | Get specific source |
| POST | `/api/sources` | Create new source |
| PUT | `/api/sources/{id}` | Update source |
| DELETE | `/api/sources/{id}` | Delete source |

### Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sources/{id}/test-connection` | Test API connection |
| POST | `/api/sources/{id}/trigger` | Trigger manual ingestion |
| GET | `/api/sources/{id}/logs` | View ingestion logs |

---

## 📦 Request Examples

### 1. Create Source - Public API (No Auth)
```bash
curl -X POST http://localhost:5082/api/sources \
  -H "Content-Type: application/json" \
  -d '{
    "name": "JSONPlaceholder Users",
    "apiType": "REST",
    "endpointUrl": "https://jsonplaceholder.typicode.com/users",
    "httpMethod": "GET",
    "authType": "None",
    "responsePath": "$",
    "enabled": true
  }'
```

### 2. Create Source - API Key Auth
```bash
curl -X POST http://localhost:5082/api/sources \
  -H "Content-Type: application/json" \
  -d '{
    "name": "NewsAPI Headlines",
    "endpointUrl": "https://newsapi.org/v2/top-headlines",
    "httpMethod": "GET",
    "authType": "ApiKey",
    "authConfig": {
      "key": "your-api-key-here",
      "param": "apiKey",
      "in": "query"
    },
    "queryParams": "{\"country\":\"us\"}",
    "responsePath": "$.articles[*]",
    "fieldMappings": "{\"title\":\"title\",\"content\":\"description\"}",
    "enabled": true
  }'
```

### 3. Create Source - Bearer Token
```bash
curl -X POST http://localhost:5082/api/sources \
  -H "Content-Type: application/json" \
  -d '{
    "name": "GitHub Issues",
    "endpointUrl": "https://api.github.com/repos/owner/repo/issues",
    "httpMethod": "GET",
    "authType": "Bearer",
    "authConfig": {
      "token": "ghp_yourGitHubToken"
    },
    "customHeaders": "{\"Accept\":\"application/vnd.github+json\"}",
    "responsePath": "$",
    "enabled": false
  }'
```

### 4. Create Source - Basic Auth
```bash
curl -X POST http://localhost:5082/api/sources \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Protected API",
    "endpointUrl": "https://api.example.com/data",
    "httpMethod": "GET",
    "authType": "Basic",
    "authConfig": {
      "username": "user@example.com",
      "password": "secretpassword"
    },
    "enabled": true
  }'
```

### 5. Test Connection
```bash
curl -X POST http://localhost:5082/api/sources/{id}/test-connection
```

### 6. Trigger Ingestion
```bash
curl -X POST http://localhost:5082/api/sources/{id}/trigger
```

### 7. Get Logs
```bash
curl http://localhost:5082/api/sources/{id}/logs?page=1&pageSize=10
```

---

## 🔑 Authentication Types

### None
```json
{
  "authType": "None"
}
```

### Basic Auth
```json
{
  "authType": "Basic",
  "authConfig": {
    "username": "user",
    "password": "pass"
  }
}
```

### Bearer Token
```json
{
  "authType": "Bearer",
  "authConfig": {
    "token": "your-bearer-token"
  }
}
```

### API Key (Header)
```json
{
  "authType": "ApiKey",
  "authConfig": {
    "key": "your-api-key",
    "header": "X-API-Key",
    "in": "header"
  }
}
```

### API Key (Query Parameter)
```json
{
  "authType": "ApiKey",
  "authConfig": {
    "key": "your-api-key",
    "param": "api_key",
    "in": "query"
  }
}
```

### OAuth2
```json
{
  "authType": "OAuth2",
  "authConfig": {
    "access_token": "your-access-token"
  }
}
```

---

## 🗺️ JSONPath Examples

### Root Array
```json
"responsePath": "$"
```
For: `[{...}, {...}]`

### Nested Array
```json
"responsePath": "$.data"
```
For: `{"data": [{...}, {...}]}`

### Array Property with Wildcard
```json
"responsePath": "$.items[*]"
```
For: `{"items": [{...}, {...}]}`

### Multi-level Nesting
```json
"responsePath": "$.response.results[*]"
```
For: `{"response": {"results": [{...}, {...}]}}`

---

## 🎨 Field Mapping

Maps API fields to document properties:

```json
{
  "fieldMappings": {
    "title": "headline",
    "content": "articleText",
    "description": "summary",
    "published_at": "publishDate"
  }
}
```

### Auto-detected Fields
If no mapping specified, system looks for:
- **Title:** `title`, `name`, `subject`, `headline`
- **Content:** `content`, `body`, `text`, `description`
- **Description:** `description`, `summary`, `excerpt`
- **Date:** `published_at`, `publishedAt`, `createdAt`, `date`

---

## 📅 Scheduling (Phase 2)

Cron expression format:
```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
* * * * *
```

### Examples
```json
"scheduleCron": "0 * * * *"      // Every hour
"scheduleCron": "*/15 * * * *"   // Every 15 minutes
"scheduleCron": "0 0 * * *"      // Daily at midnight
"scheduleCron": "0 9 * * 1"      // Every Monday at 9am
"scheduleCron": "0 */6 * * *"    // Every 6 hours
```

---

## 📊 Response Format

### Create/Update Source Response
```json
{
  "id": "uuid",
  "name": "Source Name",
  "apiType": "REST",
  "endpointUrl": "https://...",
  "enabled": true,
  "lastRunAt": "2025-10-01T14:30:00Z",
  "lastStatus": "Success",
  "consecutiveFailures": 0,
  "createdAt": "2025-10-01T12:00:00Z",
  "updatedAt": "2025-10-01T14:30:00Z"
}
```

### Trigger Ingestion Response
```json
{
  "success": true,
  "logId": "uuid",
  "recordsFetched": 100,
  "documentsCreated": 98,
  "documentsFailed": 2,
  "executionTimeMs": 1523,
  "errorMessage": null,
  "warnings": ["Warning 1", "Warning 2"],
  "triggeredAt": "2025-10-01T14:30:00Z"
}
```

### Ingestion Log Response
```json
{
  "id": "uuid",
  "sourceId": "uuid",
  "startedAt": "2025-10-01T14:30:00Z",
  "completedAt": "2025-10-01T14:30:02Z",
  "status": "Success",
  "recordsFetched": 100,
  "documentsCreated": 98,
  "documentsFailed": 2,
  "errorsCount": 2,
  "httpStatusCode": 200,
  "responseSizeBytes": 45632,
  "executionTimeMs": 1523
}
```

---

## 🔍 Troubleshooting

### Connection Test Fails
1. Check endpoint URL is accessible
2. Verify authentication credentials
3. Check custom headers if required
4. Test with curl/Postman first

### No Documents Created
1. Verify `responsePath` extracts correct data
2. Check field mappings match API response
3. Ensure records have title or content
4. Review logs for transformation errors

### Authentication Errors
1. Verify auth type matches API requirements
2. Check credentials are encrypted correctly
3. Test with Postman to confirm credentials work
4. Review API documentation for auth format

---

## 💡 Pro Tips

1. **Test with public APIs first** (JSONPlaceholder, GitHub)
2. **Use field mappings** for complex APIs
3. **Start disabled** (`enabled: false`) until tested
4. **Monitor logs** for transformation issues
5. **Keep credentials secure** - they're encrypted automatically

---

## 🚀 PowerShell Helper Functions

```powershell
# Create source
function New-ApiSource {
    param($Name, $Url, $Path = '$')
    
    $body = @{
        name = $Name
        endpointUrl = $Url
        apiType = "REST"
        httpMethod = "GET"
        authType = "None"
        responsePath = $Path
        enabled = $true
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "http://localhost:5082/api/sources" `
        -Method Post -Body $body -ContentType "application/json"
}

# Trigger ingestion
function Start-ApiIngestion {
    param($SourceId)
    
    Invoke-RestMethod -Uri "http://localhost:5082/api/sources/$SourceId/trigger" `
        -Method Post
}

# View logs
function Get-ApiIngestionLogs {
    param($SourceId)
    
    Invoke-RestMethod -Uri "http://localhost:5082/api/sources/$SourceId/logs"
}
```

### Usage
```powershell
# Create and test
$source = New-ApiSource -Name "Test API" -Url "https://jsonplaceholder.typicode.com/posts"
Start-ApiIngestion -SourceId $source.id
Get-ApiIngestionLogs -SourceId $source.id
```

---

**Ready to ingest data from any REST API! 🎉**
