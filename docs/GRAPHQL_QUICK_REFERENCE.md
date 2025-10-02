# GraphQL API Ingestion - Quick Reference

> **Quick guide for creating and testing GraphQL API sources**

---

## 📋 Create GraphQL Source

### Simple Query (No Pagination)

```powershell
POST /api/apisources
Content-Type: application/json

{
  "name": "My GraphQL API",
  "apiType": "GraphQL",
  "endpointUrl": "https://api.example.com/graphql",
  "httpMethod": "POST",
  "authType": "Bearer",
  "graphQLQuery": "query { users { id name email } }",
  "responsePath": "users",
  "paginationType": "None",
  "enabled": true
}
```

### With Query Variables

```powershell
{
  "name": "GraphQL with Variables",
  "apiType": "GraphQL",
  "endpointUrl": "https://api.example.com/graphql",
  "graphQLQuery": "query GetUsers($limit: Int!) { users(limit: $limit) { id name } }",
  "graphQLVariables": "{\"limit\": 100}",
  "responsePath": "users",
  "paginationType": "None"
}
```

### With Cursor Pagination (Relay Spec)

```powershell
{
  "name": "GraphQL Paginated",
  "apiType": "GraphQL",
  "endpointUrl": "https://api.example.com/graphql",
  "graphQLQuery": "query GetUsers($first: Int!, $after: String) { users(first: $first, after: $after) { edges { node { id name } } pageInfo { hasNextPage endCursor } } }",
  "graphQLVariables": "{\"first\": 100}",
  "responsePath": "users.edges.node",
  "paginationType": "Cursor",
  "paginationConfig": "{\"limit\": 100, \"maxPages\": 10, \"delayMs\": 1000, \"pageInfoPath\": \"users.pageInfo\", \"firstParam\": \"first\", \"afterParam\": \"after\"}"
}
```

---

## 🔑 Authentication

### Bearer Token

```json
{
  "authType": "Bearer",
  "customHeaders": "{\"Authorization\": \"Bearer YOUR_TOKEN_HERE\"}"
}
```

### API Key (Header)

```json
{
  "authType": "ApiKey",
  "authLocation": "header",
  "headerName": "X-API-Key",
  "hasApiKey": true
}
```

---

## 🔄 Pagination Configuration

### Cursor-Based (Relay Spec)

```json
{
  "limit": 100,
  "maxPages": 10,
  "delayMs": 1000,
  "pageInfoPath": "users.pageInfo",
  "firstParam": "first",
  "afterParam": "after"
}
```

**Query Format**:
```graphql
query GetUsers($first: Int!, $after: String) {
  users(first: $first, after: $after) {
    edges {
      node {
        id
        name
      }
    }
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

### Offset-Based

```json
{
  "limit": 100,
  "maxPages": 10,
  "delayMs": 1000
}
```

**Query Format**:
```graphql
query GetUsers($offset: Int!, $limit: Int!) {
  users(offset: $offset, limit: $limit) {
    id
    name
  }
}
```

---

## 📊 Response Path Extraction

### Simple Path

```json
{
  "responsePath": "users"
}
```

**Response**:
```json
{
  "data": {
    "users": [
      { "id": 1, "name": "Alice" },
      { "id": 2, "name": "Bob" }
    ]
  }
}
```

### Nested Path

```json
{
  "responsePath": "search.edges.node"
}
```

**Response**:
```json
{
  "data": {
    "search": {
      "edges": [
        { "node": { "id": 1, "name": "Alice" } },
        { "node": { "id": 2, "name": "Bob" } }
      ]
    }
  }
}
```

---

## 🧪 Testing

### 1. Test Connection

```powershell
POST /api/apisources/{sourceId}/test
```

**Response**:
```json
{
  "success": true,
  "message": "Connection successful"
}
```

### 2. Execute Ingestion

```powershell
POST /api/apisources/{sourceId}/ingest
```

**Response**:
```json
{
  "success": true,
  "logId": "abc-123-def",
  "recordsFetched": 250,
  "documentsCreated": 250,
  "pagesProcessed": 3,
  "totalRecords": 250,
  "paginationTimeMs": 6500,
  "executionTimeMs": 8200,
  "maxPagesReached": false
}
```

### 3. Check Ingestion Log

```powershell
GET /api/api-ingestion-logs/{logId}
```

**Response**:
```json
{
  "id": "abc-123-def",
  "status": "Success",
  "recordsFetched": 250,
  "documentsCreated": 250,
  "pagesProcessed": 3,
  "paginationTimeMs": 6500,
  "paginationMetrics": "{\"page_fetch_times\":[2100,2200,2200],\"avg_page_time_ms\":2166.67}"
}
```

---

## 🚀 Real-World Examples

### GitHub GraphQL API

```json
{
  "name": "GitHub Repositories",
  "apiType": "GraphQL",
  "endpointUrl": "https://api.github.com/graphql",
  "authType": "Bearer",
  "customHeaders": "{\"User-Agent\": \"SmartCollectAPI\"}",
  "graphQLQuery": "query GetRepos($first: Int!, $after: String) { search(query: \"language:C# stars:>1000\", type: REPOSITORY, first: $first, after: $after) { repositoryCount pageInfo { hasNextPage endCursor } edges { node { ... on Repository { id name description stargazerCount url } } } } }",
  "graphQLVariables": "{\"first\": 50}",
  "responsePath": "search.edges.node",
  "paginationType": "Cursor",
  "paginationConfig": "{\"limit\": 50, \"maxPages\": 20, \"delayMs\": 2000, \"pageInfoPath\": \"search.pageInfo\"}"
}
```

### SpaceX GraphQL API

```json
{
  "name": "SpaceX Launches",
  "apiType": "GraphQL",
  "endpointUrl": "https://spacex-production.up.railway.app/",
  "authType": "None",
  "graphQLQuery": "query GetLaunches { launches(limit: 100) { id mission_name launch_date_utc rocket { rocket_name } launch_success } }",
  "responsePath": "launches",
  "paginationType": "None"
}
```

---

## ⚡ PowerShell Test Script

```powershell
# test-graphql-source.ps1

$baseUrl = "https://localhost:5001/api"

# Create source
$sourcePayload = @{
    name = "Test GraphQL Source"
    apiType = "GraphQL"
    endpointUrl = "https://api.example.com/graphql"
    graphQLQuery = "query { users { id name } }"
    responsePath = "users"
    paginationType = "None"
    enabled = $true
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "$baseUrl/apisources" `
    -Method Post `
    -Body $sourcePayload `
    -ContentType "application/json" `
    -SkipCertificateCheck

$sourceId = $response.id
Write-Host "Created source: $sourceId"

# Test connection
$testResult = Invoke-RestMethod -Uri "$baseUrl/apisources/$sourceId/test" `
    -Method Post `
    -SkipCertificateCheck

Write-Host "Connection test: $($testResult.success)"

# Execute ingestion
$ingestResult = Invoke-RestMethod -Uri "$baseUrl/apisources/$sourceId/ingest" `
    -Method Post `
    -SkipCertificateCheck

Write-Host "Ingestion success: $($ingestResult.success)"
Write-Host "Records fetched: $($ingestResult.recordsFetched)"
Write-Host "Documents created: $($ingestResult.documentsCreated)"

# Cleanup
Invoke-RestMethod -Uri "$baseUrl/apisources/$sourceId" `
    -Method Delete `
    -SkipCertificateCheck | Out-Null
```

---

## 🐛 Troubleshooting

### Error: "GraphQL errors: ..."

**Cause**: GraphQL query has syntax errors or validation issues.

**Solution**: Validate your query in GraphiQL or GraphQL Playground first.

### Error: "Failed to extract pagination info"

**Cause**: `pageInfoPath` doesn't match response structure.

**Solution**: Check your response and update `pageInfoPath`:
```json
{
  "pageInfoPath": "users.pageInfo"  // For nested pageInfo
}
```

### No records fetched

**Cause**: `responsePath` doesn't match response structure.

**Solution**: Test your query and update `responsePath`:
```json
{
  "responsePath": "data.users"  // Navigate to correct path
}
```

### Authentication failed

**Cause**: Missing or invalid Bearer token.

**Solution**: Add token to custom headers:
```json
{
  "authType": "Bearer",
  "customHeaders": "{\"Authorization\": \"Bearer YOUR_TOKEN\"}"
}
```

---

## 📚 Learn More

- **GraphQL Spec**: https://graphql.org/learn/
- **Relay Connections**: https://relay.dev/graphql/connections.htm
- **GitHub GraphQL API**: https://docs.github.com/en/graphql
- **Full Documentation**: `docs/PHASE1_DAY6_7_GRAPHQL_COMPLETE.md`

---

*Quick Reference v1.0 | Phase 1 Days 6-7*
