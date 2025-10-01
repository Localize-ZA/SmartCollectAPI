# API Sources Security Architecture 🔐

## Overview

The API Sources system implements **enterprise-grade security** for storing and managing external API credentials. All sensitive data is encrypted before storage and never exposed in plain text.

---

## 🔒 Security Features

### 1. **ASP.NET Data Protection**
- Uses Microsoft's Data Protection API (DPAPI)
- Automatic key management and rotation
- Per-application isolation
- Keys stored securely in `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`

### 2. **Credential Encryption Flow**

```mermaid
graph LR
    A[User Input] --> B[Frontend]
    B --> C[HTTPS POST]
    C --> D[API Controller]
    D --> E[AuthenticationManager]
    E --> F[Encrypt with DPAPI]
    F --> G[Database Storage]
    G --> H[Encrypted Blob]
```

**Key Points:**
- ✅ Credentials encrypted **before** database write
- ✅ Only encrypted data stored in `auth_config_encrypted` column
- ✅ Decryption only happens at API request time (in-memory)
- ✅ Frontend **never** receives decrypted credentials back

### 3. **Database Schema**

```sql
CREATE TABLE api_sources (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    endpoint_url TEXT NOT NULL,
    auth_type VARCHAR(50),
    auth_config_encrypted TEXT,  -- ⚠️ Encrypted blob only
    -- ... other fields
);
```

**Important:** The `auth_config_encrypted` column contains **base64-encoded encrypted data**, not plain JSON.

---

## 🛡️ Security Guarantees

### What's Protected:
- ✅ **Passwords** (Basic Auth)
- ✅ **API Keys** (API Key Auth)
- ✅ **Bearer Tokens** (Bearer/OAuth2)
- ✅ **Access Tokens** (OAuth 2.0)
- ✅ **Any custom auth credentials**

### What Cannot Be Done:
- ❌ Retrieve plain-text credentials via API
- ❌ View credentials in database
- ❌ Export credentials
- ❌ Copy credentials between environments (without re-encryption)

### What CAN Be Done:
- ✅ Test connection (credentials decrypted in-memory only)
- ✅ Trigger ingestion (credentials used but never exposed)
- ✅ Update credentials (old encrypted, new encrypted)
- ✅ Delete source (credentials permanently deleted)

---

## 🔐 Encryption Details

### Encryption Method
```csharp
public string EncryptCredentials(Dictionary<string, string> credentials)
{
    var json = JsonSerializer.Serialize(credentials);
    var encrypted = _protector.Protect(json);  // DPAPI encryption
    return encrypted;  // Base64-encoded encrypted data
}
```

### Decryption Method
```csharp
public Dictionary<string, string> DecryptCredentials(string encrypted)
{
    var json = _protector.Unprotect(encrypted);  // DPAPI decryption
    return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
}
```

### Encryption Scope
```csharp
_protector = dataProtectionProvider.CreateProtector("ApiIngestion.AuthConfig");
```
- Purpose: `ApiIngestion.AuthConfig`
- Isolated from other application data
- Cannot be decrypted by other parts of the application

---

## 🌐 Frontend Security

### Secure Input Form

The `CreateApiSourceModal` component implements:

1. **Password Field Masking**
   ```tsx
   <input
     type={showCredentials ? "text" : "password"}
     value={authConfig.password || ""}
   />
   ```

2. **Toggle Visibility** (Optional)
   ```tsx
   <button onClick={() => setShowCredentials(!showCredentials)}>
     {showCredentials ? <EyeOff /> : <Eye />}
   </button>
   ```

3. **HTTPS-Only Transmission**
   - All credential data sent over HTTPS
   - `Content-Type: application/json`
   - Encrypted on server before storage

4. **No Local Storage**
   - Credentials never stored in browser
   - No localStorage, sessionStorage, or cookies
   - Form cleared immediately after submission

### Security Warning Display

```tsx
<div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-4">
  <Lock className="w-5 h-5 text-amber-400" />
  <p className="text-amber-300">Credentials are encrypted</p>
  <p className="text-amber-200/80">
    All authentication credentials are encrypted using ASP.NET Data Protection
    before being stored in the database. They cannot be retrieved in plain text.
  </p>
</div>
```

---

## 🔑 Authentication Types

### 1. None
No credentials stored.

### 2. Basic Auth
**Stored (Encrypted):**
```json
{
  "username": "user@example.com",
  "password": "secretpassword"
}
```

**Applied as:**
```
Authorization: Basic dXNlckBleGFtcGxlLmNvbTpzZWNyZXRwYXNzd29yZA==
```

### 3. Bearer Token
**Stored (Encrypted):**
```json
{
  "token": "your-bearer-token-here"
}
```

**Applied as:**
```
Authorization: Bearer your-bearer-token-here
```

### 4. API Key
**Stored (Encrypted):**
```json
{
  "key": "your-api-key",
  "header": "X-API-Key",
  "in": "header"
}
```

**Applied as:**
```
X-API-Key: your-api-key
```

Or in query parameter:
```
https://api.example.com/data?api_key=your-api-key
```

### 5. OAuth 2.0
**Stored (Encrypted):**
```json
{
  "access_token": "ya29.a0AfH6SMBx..."
}
```

**Applied as:**
```
Authorization: Bearer ya29.a0AfH6SMBx...
```

---

## 🚨 Security Best Practices

### For Administrators:

1. **Principle of Least Privilege**
   - Only create sources with minimum required permissions
   - Use read-only API keys when possible
   - Rotate credentials regularly

2. **Credential Rotation**
   - Update credentials via PUT `/api/sources/{id}`
   - Old credentials are overwritten (not recoverable)
   - Test new credentials before enabling

3. **Monitoring**
   - Review ingestion logs regularly
   - Monitor `consecutive_failures` counter
   - Check for unauthorized access attempts

4. **Backup Considerations**
   - Database backups contain encrypted credentials
   - Cannot be decrypted without Data Protection keys
   - Store keys separately from database backups

5. **Environment Separation**
   - Dev/staging/prod have separate encryption keys
   - Cannot copy sources between environments
   - Must re-enter credentials per environment

### For Developers:

1. **Never Log Credentials**
   ```csharp
   // ❌ BAD
   _logger.LogInformation("Using API key: {Key}", apiKey);
   
   // ✅ GOOD
   _logger.LogInformation("Applying API key authentication");
   ```

2. **Use In-Memory Only**
   ```csharp
   // Decrypt only when needed
   var authConfig = _authManager.DecryptCredentials(source.AuthConfigEncrypted);
   // Use immediately
   ApplyAuth(request, authConfig);
   // Don't store in variables
   ```

3. **Validate Input**
   ```csharp
   if (string.IsNullOrEmpty(authConfig.GetValueOrDefault("password")))
   {
       throw new InvalidOperationException("Password is required for Basic auth");
   }
   ```

---

## 🔍 Auditing

### What's Logged:
- ✅ Source creation/update/deletion
- ✅ Ingestion attempts (success/failure)
- ✅ Connection tests
- ✅ Authentication type used

### What's NOT Logged:
- ❌ Plain-text credentials
- ❌ Decrypted tokens
- ❌ Password values
- ❌ API key values

### Audit Trail Example:
```
[INFO] Created new API source: 582c1e3c-765c-4030-ad50-61580c6f5d31 - JSONPlaceholder Test
[INFO] Applied API key authentication in header X-API-Key
[INFO] Fetched data from https://api.example.com. Status: 200, Size: 45632 bytes
```

---

## 🛠️ Credential Management

### Adding Credentials

**Via Frontend:**
1. Navigate to `/api-sources`
2. Click "Add Source"
3. Select authentication type
4. Enter credentials (masked input)
5. Submit (immediately encrypted)

**Via API:**
```bash
curl -X POST http://localhost:5082/api/sources \
  -H "Content-Type: application/json" \
  -d '{
    "name": "GitHub API",
    "endpointUrl": "https://api.github.com/repos/owner/repo",
    "authType": "Bearer",
    "authConfig": {
      "token": "ghp_YourGitHubTokenHere"
    }
  }'
```

### Updating Credentials

```bash
curl -X PUT http://localhost:5082/api/sources/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "authConfig": {
      "token": "new-token-here"
    }
  }'
```

### Deleting Credentials

```bash
curl -X DELETE http://localhost:5082/api/sources/{id}
```

Credentials are permanently deleted (no soft delete).

---

## 📊 Security Comparison

| Storage Method | Security Level | Notes |
|---------------|---------------|-------|
| **Plain Text** | ❌ None | Never use |
| **Base64** | ❌ Encoding only | Not encryption |
| **Environment Variables** | ⚠️ Low | Visible in process list |
| **Secrets Manager** | ✅ High | Cloud-based (AWS/Azure) |
| **DPAPI** | ✅ High | OS-level encryption |
| **Hardware Security Module** | ✅ Very High | Enterprise only |

**Our Implementation:** ✅ **DPAPI** (ASP.NET Data Protection)

---

## 🔄 Migration & Portability

### Key Storage Locations

**Development:**
```
%LOCALAPPDATA%\ASP.NET\DataProtection-Keys\
```

**Production (Docker):**
```
/root/.aspnet/DataProtection-Keys/
```

**Production (Azure App Service):**
```
%HOME%\ASP.NET\DataProtection-Keys\
```

### Moving Between Environments

**❌ Cannot:**
- Copy database to new server and decrypt credentials
- Export credentials for backup
- View credentials in production

**✅ Can:**
- Re-enter credentials in new environment
- Use configuration management (Terraform, Ansible)
- Automate with secret management tools

---

## 🚀 Production Deployment

### Recommended Setup:

1. **Use External Key Storage**
   ```csharp
   services.AddDataProtection()
       .PersistKeysToAzureBlobStorage(...)
       .ProtectKeysWithAzureKeyVault(...);
   ```

2. **Enable Key Rotation**
   ```csharp
   services.AddDataProtection()
       .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
   ```

3. **Monitor Key Health**
   - Check key expiration dates
   - Test decryption periodically
   - Have key recovery plan

4. **Backup Strategy**
   - Backup database (encrypted credentials)
   - Backup Data Protection keys separately
   - Document key recovery process

---

## 📚 Additional Resources

- [ASP.NET Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [DPAPI Overview](https://docs.microsoft.com/en-us/dotnet/standard/security/cryptography-model)
- [Key Management](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management)

---

## ✅ Security Checklist

- [x] Credentials encrypted at rest (DPAPI)
- [x] Credentials encrypted in transit (HTTPS)
- [x] No plain-text logging
- [x] No credential export functionality
- [x] Frontend masking (password fields)
- [x] Per-purpose encryption keys
- [x] Audit trail for all operations
- [x] Secure deletion (no recovery)
- [x] Environment isolation
- [x] Input validation

**Status: ✅ Production Ready**

---

**Remember:** Security is not just about encryption—it's about the entire lifecycle of credential management. This system is designed to protect credentials from creation to deletion, with no weak points in between.
