
---

# 🚀 Robust API Ingestion Service - Complete Architecture Plan

## 📋 Overview

A **scheduled background service** that fetches data from external APIs (REST, GraphQL, SOAP), transforms the data, and feeds it into your existing document processing pipeline.

---

## 🏗️ Architecture Design

### **System Type**: Background Service (IHostedService)
- **Location**: Within main C# API (`SmartCollectAPI.Server`)
- **Reason**: Direct access to DocumentProcessingPipeline and database context
- **Pattern**: Background worker with scheduled jobs

---

## 📊 Database Schema

### **Table 1: `api_sources`**
```sql
CREATE TABLE api_sources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    
    -- API Configuration
    api_type VARCHAR(50) NOT NULL, -- 'REST', 'GraphQL', 'SOAP'
    endpoint_url TEXT NOT NULL,
    http_method VARCHAR(10) DEFAULT 'GET', -- GET, POST, etc.
    
    -- Authentication
    auth_type VARCHAR(50), -- 'None', 'Basic', 'Bearer', 'OAuth2', 'ApiKey'
    auth_config_encrypted TEXT, -- Encrypted JSON with credentials
    
    -- Headers & Body
    custom_headers JSONB, -- {"Authorization": "Bearer ...", ...}
    request_body TEXT, -- For POST/PUT requests
    query_params JSONB, -- {"page": "1", "limit": "100"}
    
    -- Data Transformation
    response_path VARCHAR(500), -- JSONPath: "$.data.items[*]"
    field_mappings JSONB, -- {"title": "$.headline", "content": "$.body"}
    
    -- Pagination
    pagination_type VARCHAR(50), -- 'None', 'Offset', 'Cursor', 'Page'
    pagination_config JSONB, -- {"limit": 100, "page_param": "page"}
    
    -- Scheduling
    schedule_cron VARCHAR(100), -- "0 */6 * * *" (every 6 hours)
    enabled BOOLEAN DEFAULT true,
    
    -- Tracking
    last_run_at TIMESTAMPTZ,
    next_run_at TIMESTAMPTZ,
    last_status VARCHAR(50), -- 'success', 'failed', 'partial'
    consecutive_failures INTEGER DEFAULT 0,
    
    -- Metadata
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
```

### **Table 2: `api_ingestion_logs`**
```sql
CREATE TABLE api_ingestion_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id UUID NOT NULL REFERENCES api_sources(id) ON DELETE CASCADE,
    
    started_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL, -- 'running', 'success', 'failed', 'partial'
    
    records_fetched INTEGER DEFAULT 0,
    documents_created INTEGER DEFAULT 0,
    errors_count INTEGER DEFAULT 0,
    
    error_message TEXT,
    error_details JSONB,
    
    execution_time_ms INTEGER,
    
    INDEX idx_source_id (source_id),
    INDEX idx_started_at (started_at)
);
```

---

## 🔧 Core Components

### **1. API Client Interfaces**

```csharp
public interface IApiClient
{
    Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken ct);
    Task<bool> TestConnectionAsync(ApiSource source, CancellationToken ct);
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string RawResponse { get; set; }
    public object ParsedData { get; set; }
    public int RecordCount { get; set; }
    public string ErrorMessage { get; set; }
}
```

### **2. Protocol Handlers**

```csharp
// REST API Client
public class RestApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestApiClient> _logger;
    
    // Supports: GET, POST, PUT, DELETE, PATCH
    // Handles: Headers, Query Params, Request Body
    // Features: Retry policies, timeout, compression
}

// GraphQL Client
public class GraphQLApiClient : IApiClient
{
    private readonly GraphQLHttpClient _client;
    
    // Supports: Queries, Mutations
    // Features: Variable binding, fragment support
}

// SOAP Client
public class SoapApiClient : IApiClient
{
    // Uses: System.ServiceModel for SOAP 1.1/1.2
    // Features: WSDL parsing, envelope construction
}
```

### **3. Data Transformer**

```csharp
public class DataTransformer
{
    public List<StagingDocument> Transform(
        ApiResponse response,
        ApiSource source)
    {
        // 1. Extract records using JSONPath/XPath
        // 2. Map fields according to configuration
        // 3. Generate sourceUri with API identifier
        // 4. Create StagingDocuments
        // 5. Preserve metadata (source, fetch time)
    }
}
```

### **4. Ingestion Scheduler (Background Service)**

```csharp
public class ApiIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiIngestionService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CheckAndRunScheduledJobs(ct);
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
    
    private async Task CheckAndRunScheduledJobs(CancellationToken ct)
    {
        // 1. Query enabled sources with next_run_at <= NOW
        // 2. For each source, run ingestion job
        // 3. Update next_run_at based on cron
        // 4. Log results
    }
}
```

### **5. Authentication Manager**

```csharp
public class AuthenticationManager
{
    private readonly IDataProtector _protector;
    
    public void ApplyAuthentication(
        HttpRequestMessage request,
        ApiSource source)
    {
        var decrypted = DecryptAuthConfig(source.AuthConfigEncrypted);
        
        switch (source.AuthType)
        {
            case "Basic":
                // Add Basic Auth header
                break;
            case "Bearer":
                // Add Bearer token
                break;
            case "OAuth2":
                // Handle OAuth2 flow
                break;
            case "ApiKey":
                // Add API key to header or query
                break;
        }
    }
}
```

---

## 📅 Implementation Phases

### **Phase 1: Foundation (Week 1)**
- ✅ Database schema migration
- ✅ Entity models (ApiSource, ApiIngestionLog)
- ✅ REST API client with basic auth
- ✅ Simple JSON transformation (flat objects)
- ✅ Manual trigger endpoint for testing

### **Phase 2: Scheduling (Week 2)**
- ✅ Background service with cron scheduling
- ✅ Automatic job execution
- ✅ Retry policies with Polly
- ✅ Error handling and logging
- ✅ Email alerts for failures

### **Phase 3: Advanced Protocols (Week 3)**
- ✅ GraphQL client implementation
- ✅ SOAP client implementation
- ✅ Advanced transformations (nested objects, arrays)
- ✅ Pagination support (all types)

### **Phase 4: Frontend UI (Week 4)**
- ✅ API Sources management page
- ✅ CRUD operations for sources
- ✅ Test connection feature
- ✅ Manual trigger button
- ✅ Ingestion logs viewer
- ✅ Real-time status monitoring

### **Phase 5: Advanced Features (Future)**
- 🔄 Webhooks (receive push notifications)
- 🔄 Incremental sync (track changes)
- 🔄 Deduplication (avoid re-ingesting same data)
- 🔄 Data validation rules
- 🔄 Custom transformation scripts

---

## 🛠️ NuGet Packages Required

```xml
<PackageReference Include="Polly" Version="8.4.0" />
<PackageReference Include="NCrontab" Version="3.3.3" />
<PackageReference Include="GraphQL.Client" Version="6.1.0" />
<PackageReference Include="GraphQL.Client.Serializer.SystemTextJson" Version="6.1.0" />
<PackageReference Include="System.ServiceModel.Http" Version="8.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

---

## 🔐 Security Measures

1. **Credential Encryption**: Use ASP.NET Core Data Protection API
2. **HTTPS Only**: All external API calls over TLS
3. **Certificate Validation**: Validate SSL certificates
4. **Input Sanitization**: Clean data before document creation
5. **Rate Limiting**: Respect external API limits
6. **Secrets Management**: Never log credentials
7. **IP Whitelisting**: For webhook endpoints (Phase 5)

---

## 📈 Monitoring & Observability

- **Metrics**: Records fetched, documents created, errors
- **Logs**: Structured logging with Serilog
- **Alerts**: Email notifications for failures
- **Dashboard**: Real-time ingestion status in UI
- **History**: 90-day retention of ingestion logs

---

## 🎯 Example Use Cases

### **Use Case 1: News API**
```json
{
  "name": "NewsAPI.org",
  "api_type": "REST",
  "endpoint_url": "https://newsapi.org/v2/everything",
  "auth_type": "ApiKey",
  "auth_config": {"api_key": "YOUR_KEY"},
  "query_params": {"q": "technology", "pageSize": 100},
  "response_path": "$.articles[*]",
  "field_mappings": {
    "title": "$.title",
    "content": "$.content",
    "metadata": {"author": "$.author", "published": "$.publishedAt"}
  },
  "schedule_cron": "0 */2 * * *"
}
```

### **Use Case 2: GitHub GraphQL API**
```json
{
  "name": "GitHub Issues",
  "api_type": "GraphQL",
  "endpoint_url": "https://api.github.com/graphql",
  "auth_type": "Bearer",
  "request_body": "query { repository(owner:\"facebook\", name:\"react\") { issues(first:100) { nodes { title body } } } }",
  "response_path": "$.data.repository.issues.nodes[*]",
  "schedule_cron": "0 0 * * *"
}
```

### **Use Case 3: Legacy SOAP Service**
```json
{
  "name": "Enterprise Data Service",
  "api_type": "SOAP",
  "endpoint_url": "https://api.company.com/DataService.asmx",
  "auth_type": "Basic",
  "request_body": "<soap:Envelope>...</soap:Envelope>",
  "schedule_cron": "0 6 * * *"
}
```

---

## 🚦 Implementation Priority

1. **Phase 1** - Database schema and REST client (MVP)
2. **Phase 2** - Scheduling and automation
3. **Phase 4** - Frontend UI (parallel with Phase 3)
4. **Phase 3** - Advanced protocols (GraphQL, SOAP)
5. **Phase 5** - Advanced features (as needed)

This is a **production-grade architecture** that integrates seamlessly with the existing SmartCollect pipeline.
