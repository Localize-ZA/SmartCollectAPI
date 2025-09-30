export const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5082";

// Client-side health check (kept for backward compatibility but prefer server actions)
export async function getHealth(): Promise<{ status: string; ts: string }> {
  const res = await fetch(`${API_BASE}/health/basic`, { 
    cache: "no-store",
    // Add timeout for client requests
    signal: AbortSignal.timeout(5000)
  });
  if (!res.ok) throw new Error(`Health check failed: ${res.status}`);
  return res.json();
}

export interface IngestResponse {
  job_id: string;
  sha256: string;
  source_uri: string;
}

export async function uploadDocument(file: File, notifyEmail?: string): Promise<IngestResponse> {
  const form = new FormData();
  form.append("file", file);
  if (notifyEmail) form.append("notify_email", notifyEmail);
  const res = await fetch(`${API_BASE}/api/ingest`, {
    method: "POST",
    body: form,
  });
  if (!res.ok && res.status !== 202) {
    const text = await res.text();
    throw new Error(`Upload failed (${res.status}): ${text}`);
  }
  return res.json();
}

export interface MultiIngestResponse {
  results: IngestResponse[];
  errors: { fileName: string; error: string }[];
  totalFiles: number;
  successCount: number;
  errorCount: number;
}

// Bulk upload using the server's bulk endpoint (more efficient)
export async function uploadMultipleDocumentsBulk(
  files: File[], 
  notifyEmail?: string
): Promise<MultiIngestResponse> {
  const form = new FormData();
  files.forEach(file => {
    form.append("files", file);
  });
  if (notifyEmail) form.append("notify_email", notifyEmail);
  
  const res = await fetch(`${API_BASE}/api/ingest/bulk`, {
    method: "POST",
    body: form,
  });
  
  if (!res.ok && res.status !== 202) {
    const text = await res.text();
    throw new Error(`Bulk upload failed (${res.status}): ${text}`);
  }
  
  return res.json();
}

// Client-side concurrent upload (with progress callbacks)
export async function uploadMultipleDocuments(
  files: File[], 
  notifyEmail?: string,
  onProgress?: (progress: { fileName: string; status: 'uploading' | 'success' | 'error'; result?: IngestResponse; error?: string }) => void
): Promise<MultiIngestResponse> {
  const results: IngestResponse[] = [];
  const errors: { fileName: string; error: string }[] = [];
  
  // Upload files concurrently but with a limit to avoid overwhelming the server
  const concurrencyLimit = 3;
  const chunks = [];
  for (let i = 0; i < files.length; i += concurrencyLimit) {
    chunks.push(files.slice(i, i + concurrencyLimit));
  }
  
  for (const chunk of chunks) {
    const promises = chunk.map(async (file) => {
      try {
        onProgress?.({ fileName: file.name, status: 'uploading' });
        const result = await uploadDocument(file, notifyEmail);
        onProgress?.({ fileName: file.name, status: 'success', result });
        results.push(result);
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        onProgress?.({ fileName: file.name, status: 'error', error: errorMessage });
        errors.push({ fileName: file.name, error: errorMessage });
      }
    });
    
    await Promise.all(promises);
  }
  
  return {
    results,
    errors,
    totalFiles: files.length,
    successCount: results.length,
    errorCount: errors.length
  };
}

export interface ProcessingStats {
  totalDocuments: number;
  documentsWithEmbeddings: number;
  processedToday: number;
  stagingStatus: Record<string, number>;
}

export async function getProcessingStats(): Promise<ProcessingStats> {
  const res = await fetch(`${API_BASE}/api/documents/stats`, { cache: "no-store" });
  if (!res.ok) throw new Error(`Stats request failed: ${res.status}`);
  return res.json();
}

export interface StagingDocumentSummary {
  id: string;
  jobId: string;
  sourceUri: string;
  mime?: string | null;
  sha256?: string | null;
  status: string;
  attempts: number;
  createdAt: string;
  updatedAt: string;
  rawMetadata?: unknown;
  normalized?: unknown;
}

export async function getStagingDocuments(status?: string): Promise<StagingDocumentSummary[]> {
  const url = new URL(`${API_BASE}/api/documents/staging`);
  if (status) url.searchParams.set("status", status);
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`Staging request failed: ${res.status}`);
  return res.json();
}

export interface DocumentSummary {
  id: string;
  sourceUri: string;
  mime?: string | null;
  sha256?: string | null;
  createdAt: string;
  hasEmbedding: boolean;
}

export interface PagedDocuments {
  items: DocumentSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export async function getDocuments(page = 1, pageSize = 10): Promise<PagedDocuments> {
  const url = new URL(`${API_BASE}/api/documents`);
  url.searchParams.set("page", page.toString());
  url.searchParams.set("pageSize", pageSize.toString());
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`Documents request failed: ${res.status}`);
  return res.json();
}

export interface DocumentDetail {
  id: string;
  sourceUri: string;
  mime?: string | null;
  sha256?: string | null;
  canonical: unknown;
  createdAt: string;
  updatedAt: string;
  embedding?: number[] | null | Record<string, unknown>;
}

export async function getDocument(id: string): Promise<DocumentDetail> {
  const res = await fetch(`${API_BASE}/api/documents/${id}`, { cache: "no-store" });
  if (res.status === 404) {
    throw new Error("Document not found");
  }
  if (!res.ok) throw new Error(`Document request failed: ${res.status}`);
  return res.json();
}

export async function searchDocuments(query: string, limit = 10): Promise<DocumentSummary[]> {
  const res = await fetch(`${API_BASE}/api/documents/search`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ query, limit })
  });
  if (!res.ok) throw new Error(`Search failed: ${res.status}`);
  return res.json();
}

export async function getSimilarDocuments(id: string, limit = 5): Promise<DocumentSummary[]> {
  const url = new URL(`${API_BASE}/api/documents/${id}/similar`);
  url.searchParams.set("limit", limit.toString());
  const res = await fetch(url, { cache: "no-store" });
  if (res.status === 404) {
    throw new Error("Document not found");
  }
  if (!res.ok) throw new Error(`Similarity request failed: ${res.status}`);
  return res.json();
}

// Microservices monitoring
export interface MicroserviceStatus {
  name: string;
  url: string;
  status: 'healthy' | 'unhealthy' | 'unknown';
  responseTime?: number;
  lastChecked: string;
  version?: string;
  error?: string;
}

export async function checkMicroserviceHealth(name: string, url: string): Promise<MicroserviceStatus> {
  const startTime = Date.now();
  try {
    // Determine health endpoint based on service type
    const healthPath = name === 'spaCy NLP Service' ? '/health' : '/health/basic';
    const healthUrl = `${url}${healthPath}`;
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000); // 5 second timeout
    
    const res = await fetch(healthUrl, {
      cache: "no-store",
      signal: controller.signal
    });
    
    clearTimeout(timeoutId);
    const responseTime = Date.now() - startTime;
    
    if (res.ok) {
      const data = await res.json();
      // Handle different response formats
      const isHealthy = data.status === 'healthy' || data.status === 'ok';
      return {
        name,
        url,
        status: isHealthy ? 'healthy' : 'unhealthy',
        responseTime,
        lastChecked: new Date().toISOString(),
        version: data.version || '1.0.0'
      };
    } else {
      return {
        name,
        url,
        status: 'unhealthy',
        responseTime,
        lastChecked: new Date().toISOString(),
        error: `HTTP ${res.status}`
      };
    }
  } catch (error) {
    const responseTime = Date.now() - startTime;
    return {
      name,
      url,
      status: 'unhealthy',
      responseTime,
      lastChecked: new Date().toISOString(),
      error: error instanceof Error ? error.message : String(error)
    };
  }
}

export async function getAllMicroservicesHealth(): Promise<MicroserviceStatus[]> {
  const microservices = [
    { name: 'Main API', url: API_BASE },
    { name: 'SMTP Service', url: 'http://localhost:5083' },
    { name: 'spaCy NLP Service', url: 'http://localhost:5084' }
  ];

  const healthChecks = microservices.map(service => 
    checkMicroserviceHealth(service.name, service.url)
  );

  return Promise.all(healthChecks);
}
