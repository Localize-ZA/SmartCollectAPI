# Google Cloud Services Setup Guide

This guide walks you through setting up all the Google Cloud services required for SmartCollectAPI.

## Prerequisites

1. Google Cloud Platform account
2. Billing enabled on your GCP project
3. `gcloud` CLI installed and authenticated

## 1. Create and Configure Project

```bash
# Create new project (optional)
gcloud projects create your-smartcollect-project --name="SmartCollect API"

# Set active project
gcloud config set project your-smartcollect-project

# Enable billing (replace BILLING_ACCOUNT_ID)
gcloud beta billing projects link your-smartcollect-project --billing-account=YOUR_BILLING_ACCOUNT_ID
```

## 2. Enable Required APIs

```bash
# Enable all required Google Cloud APIs
gcloud services enable documentai.googleapis.com
gcloud services enable vision.googleapis.com  
gcloud services enable language.googleapis.com
gcloud services enable aiplatform.googleapis.com
gcloud services enable gmail.googleapis.com
gcloud services enable storage.googleapis.com
```

## 3. Create Service Account

```bash
# Create service account for SmartCollect API
gcloud iam service-accounts create smartcollect-api \
    --display-name="SmartCollect API Service Account" \
    --description="Service account for SmartCollect document processing"

# Get the service account email
export SERVICE_ACCOUNT_EMAIL=smartcollect-api@your-smartcollect-project.iam.gserviceaccount.com
```

## 4. Grant Required Permissions

```bash
# Document AI permissions
gcloud projects add-iam-policy-binding your-smartcollect-project \
    --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" \
    --role="roles/documentai.apiUser"

# Vision AI permissions  
gcloud projects add-iam-policy-binding your-smartcollect-project \
    --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" \
    --role="roles/vision.apiUser"

# Natural Language AI permissions
gcloud projects add-iam-policy-binding your-smartcollect-project \
    --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" \
    --role="roles/language.apiUser"

# Vertex AI permissions
gcloud projects add-iam-policy-binding your-smartcollect-project \
    --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" \
    --role="roles/aiplatform.user"

# Storage permissions (if using Cloud Storage)
gcloud projects add-iam-policy-binding your-smartcollect-project \
    --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" \
    --role="roles/storage.objectAdmin"
```

## 5. Create Service Account Key

```bash
# Create and download service account key
gcloud iam service-accounts keys create smartcollect-credentials.json \
    --iam-account=$SERVICE_ACCOUNT_EMAIL

# Note: Store this file securely and update your appsettings.json
```

## 6. Set Up Document AI Processor

```bash
# List available processor types
gcloud ai processors types list --location=us

# Create a general document processor
gcloud ai processors create \
    --display-name="SmartCollect Document Processor" \
    --location=us \
    --type=OCR_PROCESSOR

# Note the processor ID from the output for your configuration
```

## 7. Gmail API Setup (For Notifications)

### Option A: Service Account (Domain-wide Delegation)
If you have G Workspace and want server-to-server access:

1. Go to Google Admin Console
2. Security → API Controls → Domain-wide Delegation
3. Add your service account client ID with Gmail scope

### Option B: OAuth 2.0 (Recommended for Development)
1. Go to Google Cloud Console → APIs & Credentials
2. Create OAuth 2.0 Client ID (Web application)
3. Add authorized redirect URIs
4. Download client secrets JSON
5. Implement OAuth flow in your application

For testing, you can generate a refresh token:

```bash
# Use Google OAuth 2.0 Playground or implement OAuth flow
# Store refresh token securely in your configuration
```

## 8. Update Configuration

Update your `appsettings.json`:

```json
{
  "GoogleCloud": {
    "ProjectId": "your-smartcollect-project",
    "Location": "us",
    "ProcessorId": "your-processor-id-from-step-6",
    "CredentialsPath": "/path/to/smartcollect-credentials.json"
  },
  "Gmail": {
    "CredentialsPath": "/path/to/gmail-credentials.json",
    "FromEmail": "noreply@yourdomain.com"
  }
}
```

## 9. Test the Setup

Create a simple test to verify your setup:

```bash
# Test Document AI
curl -X POST "https://documentai.googleapis.com/v1/projects/your-project/locations/us/processors/your-processor-id:process" \
  -H "Authorization: Bearer $(gcloud auth print-access-token)" \
  -H "Content-Type: application/json" \
  -d '{
    "rawDocument": {
      "content": "'$(base64 -i test.pdf)'",
      "mimeType": "application/pdf"
    }
  }'
```

## 10. Cost Considerations

### Pricing Overview (as of 2024)
- **Document AI**: $1.50 per 1,000 pages for general parsing
- **Vision API**: $1.50 per 1,000 images for OCR
- **Natural Language API**: $1.00 per 1,000 text records for entity extraction
- **Vertex AI Embeddings**: $0.00025 per 1,000 characters processed
- **Gmail API**: Free for normal usage limits

### Cost Optimization Tips
1. **Batch processing**: Group multiple requests where possible
2. **Caching**: Store API responses to avoid reprocessing
3. **Filtering**: Only process files that need AI services
4. **Monitoring**: Set up billing alerts and quotas

## 11. Quotas and Limits

Default quotas (may vary by region):
- Document AI: 600 requests per minute
- Vision API: 1800 requests per minute  
- Natural Language API: 600 requests per minute
- Vertex AI: Varies by model and region

Request quota increases if needed:
```bash
gcloud services quotas list --service=documentai.googleapis.com
# Contact support for quota increases
```

## 12. Monitoring and Logging

Enable Cloud Logging and Monitoring:

```bash
# View API usage
gcloud logging read "resource.type=gce_instance AND jsonPayload.service_name=documentai.googleapis.com" --limit=10

# Set up monitoring alerts
gcloud alpha monitoring policies create --policy-from-file=monitoring-policy.yaml
```

## 13. Security Best Practices

1. **Rotate service account keys** regularly
2. **Use least privilege** principle for IAM roles
3. **Enable audit logging** for API access
4. **Monitor unusual usage** patterns
5. **Secure credential storage** (never commit keys to code)

## Troubleshooting

### Common Issues

1. **403 Forbidden**: Check IAM permissions and API enablement
2. **429 Rate Limited**: Implement exponential backoff
3. **404 Processor Not Found**: Verify processor ID and location
4. **Invalid Credentials**: Check service account key and project ID

### Debug Commands

```bash
# Test authentication
gcloud auth application-default print-access-token

# Check service account permissions  
gcloud projects get-iam-policy your-smartcollect-project --flatten="bindings[].members" --filter="bindings.members:smartcollect-api@*"

# View audit logs
gcloud logging read "protoPayload.serviceName=documentai.googleapis.com" --limit=5
```

## Production Deployment

For production deployment:

1. Use **Workload Identity** instead of service account keys
2. Set up **VPC Service Controls** for additional security
3. Configure **Cloud KMS** for encryption at rest
4. Implement **Cloud Armor** for DDoS protection
5. Use **Cloud Load Balancer** for high availability

This completes the Google Cloud setup for SmartCollectAPI. Your system will now be able to leverage all Google AI services for intelligent document processing.