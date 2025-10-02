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
  embeddingProvider?: string | null;
  embeddingDimensions?: number | null;
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
  embeddingProvider?: string | null;
  embeddingDimensions?: number | null;
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
    { name: 'spaCy NLP Service', url: 'http://localhost:5084' },
    { name: 'Embeddings Service', url: 'http://localhost:8001' },
    { name: 'OCR Service', url: 'http://localhost:8002' },
    { name: 'Language Detection', url: 'http://localhost:8004' }
  ];

  const healthChecks = microservices.map(service => 
    checkMicroserviceHealth(service.name, service.url)
  );

  return Promise.all(healthChecks);
}

// ===========================
// Chunk Search (Phase 3)
// ===========================

export interface ChunkSearchResult {
  chunkId: number;
  documentId: string;
  chunkIndex: number;
  content: string;
  similarity: number;
  documentUri: string;
}

export interface ChunkSearchRequest {
  query: string;
  provider?: string;
  limit?: number;
  similarityThreshold?: number;
}

export interface ChunkSearchResponse {
  query: string;
  provider: string;
  resultCount: number;
  results: ChunkSearchResult[];
}

export async function searchChunks(request: ChunkSearchRequest): Promise<ChunkSearchResponse> {
  const res = await fetch(`${API_BASE}/api/ChunkSearch/search`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      query: request.query,
      provider: request.provider || "sentence-transformers",
      limit: request.limit || 10,
      similarityThreshold: request.similarityThreshold || 0.7
    }),
    cache: "no-store"
  });
  
  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: "Search failed" }));
    throw new Error(error.error || `Chunk search failed: ${res.status}`);
  }
  
  return res.json();
}

export interface DocumentChunk {
  id: number;
  documentId: string;
  chunkIndex: number;
  content: string;
  startPosition: number;
  endPosition: number;
  tokenCount: number;
  createdAt: string;
}

export async function getDocumentChunks(documentId: string): Promise<DocumentChunk[]> {
  const res = await fetch(`${API_BASE}/api/ChunkSearch/document/${documentId}`, {
    cache: "no-store"
  });
  
  if (res.status === 404) {
    throw new Error("Document not found or has no chunks");
  }
  
  if (!res.ok) {
    throw new Error(`Failed to get chunks: ${res.status}`);
  }
  
  return res.json();
}

export interface HybridSearchRequest {
  query: string;
  provider?: string;
  limit?: number;
  similarityThreshold?: number;
  semanticWeight?: number;
  textWeight?: number;
}

export interface HybridSearchResponse {
  query: string;
  provider: string;
  resultCount: number;
  results: ChunkSearchResult[];
}

export async function hybridSearchChunks(request: HybridSearchRequest): Promise<HybridSearchResponse> {
  const res = await fetch(`${API_BASE}/api/ChunkSearch/hybrid-search`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      query: request.query,
      provider: request.provider || "sentence-transformers",
      limit: request.limit || 10,
      similarityThreshold: request.similarityThreshold || 0.7,
      semanticWeight: request.semanticWeight || 0.7,
      textWeight: request.textWeight || 0.3
    }),
    cache: "no-store"
  });
  
  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: "Hybrid search failed" }));
    throw new Error(error.error || `Hybrid search failed: ${res.status}`);
  }
  
  return res.json();
}

// ===========================
// Language Detection (Phase 4)
// ===========================

export interface LanguageCandidate {
  language: string;
  languageName: string;
  confidence: number;
  isoCode639_1?: string;
  isoCode639_3?: string;
}

export interface LanguageDetectionResult {
  detectedLanguage: LanguageCandidate;
  allCandidates: LanguageCandidate[];
  textLength: number;
}

export async function detectLanguage(text: string, minConfidence = 0.0): Promise<LanguageDetectionResult> {
  const res = await fetch("http://localhost:8004/detect", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text, min_confidence: minConfidence }),
    cache: "no-store",
    signal: AbortSignal.timeout(10000)
  });
  
  if (!res.ok) {
    throw new Error(`Language detection failed: ${res.status}`);
  }
  
  return res.json();
}

export interface SupportedLanguage {
  code: string;
  name: string;
  isoCode639_1?: string;
  isoCode639_3?: string;
}

export async function getSupportedLanguages(): Promise<SupportedLanguage[]> {
  const res = await fetch("http://localhost:8004/languages", {
    cache: "no-store",
    signal: AbortSignal.timeout(5000)
  });
  
  if (!res.ok) {
    throw new Error(`Failed to get languages: ${res.status}`);
  }
  
  return res.json();
}

// ===========================
// API Sources Management
// ===========================

export interface ApiSource {
  id: string;
  name: string;
  description?: string;
  apiType: string;
  endpointUrl: string;
  httpMethod: string;
  authType?: string;
  authLocation?: string;
  headerName?: string;
  queryParam?: string;
  hasApiKey?: boolean;
  customHeaders?: string;
  requestBody?: string;
  queryParams?: string;
  responsePath?: string;
  fieldMappings?: string;
  paginationType?: string;
  paginationConfig?: string;
  scheduleCron?: string;
  enabled: boolean;
  lastRunAt?: string;
  lastUsedAt?: string;
  lastStatus?: string;
  consecutiveFailures: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateApiSourceDto {
  name: string;
  description?: string;
  apiType?: string;
  endpointUrl: string;
  httpMethod?: string;
  authType?: string;
  authConfig?: Record<string, string>;
  authLocation?: string;
  headerName?: string;
  queryParam?: string;
  apiKey?: string;
  customHeaders?: string;
  requestBody?: string;
  queryParams?: string;
  responsePath?: string;
  fieldMappings?: string;
  paginationType?: string;
  paginationConfig?: string;
  scheduleCron?: string;
  enabled?: boolean;
}

export interface ApiIngestionLog {
  id: string;
  sourceId: string;
  startedAt: string;
  completedAt?: string;
  status?: string;
  recordsFetched: number;
  documentsCreated: number;
  documentsFailed: number;
  errorsCount: number;
  errorMessage?: string;
  httpStatusCode?: number;
  responseSizeBytes?: number;
  executionTimeMs?: number;
  pagesProcessed?: number;
  totalPages?: number;
}

export async function getApiSources(filters?: {
  apiType?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<ApiSource[]> {
  const url = new URL(`${API_BASE}/api/sources`);
  if (filters?.apiType) url.searchParams.set("apiType", filters.apiType);
  if (filters?.enabled !== undefined) url.searchParams.set("enabled", String(filters.enabled));
  if (filters?.page) url.searchParams.set("page", String(filters.page));
  if (filters?.pageSize) url.searchParams.set("pageSize", String(filters.pageSize));
  
  const res = await fetch(url, { cache: "no-store" });
  
  if (!res.ok) {
    throw new Error(`Failed to get API sources: ${res.status}`);
  }
  
  return res.json();
}

export async function getApiSource(id: string): Promise<ApiSource> {
  const res = await fetch(`${API_BASE}/api/sources/${id}`, { cache: "no-store" });
  
  if (res.status === 404) {
    throw new Error("API source not found");
  }
  
  if (!res.ok) {
    throw new Error(`Failed to get API source: ${res.status}`);
  }
  
  return res.json();
}

\nexport interface ApiSourceAutoFillSuggestion {\n  field: string;\n  value?: string;\n  confidence: number;\n  notes?: string;\n}\n\nexport interface ApiSourceAutoFillResult {\n  suggestions: ApiSourceAutoFillSuggestion[];\n  warnings: string[];\n  sampleSnippet?: string;\n}\n\nexport async function autoFillApiSource(docsUrl: string, notes?: string): Promise<ApiSourceAutoFillResult> {\n  const payload: Record<string, unknown> = { docsUrl };\n  if (notes) payload.notes = notes;\n\n  const res = await fetch(`${API_BASE}/api/sources/auto-fill`, {\n    method: "POST",\n    headers: { "Content-Type": "application/json" },\n    signal: AbortSignal.timeout(15000),\n    body: JSON.stringify(payload),\n  });\n\n  const result = await res.json().catch(() => ({ suggestions: [], warnings: [] }));\n\n  if (!res.ok) {\n    throw new Error(result?.error || `Failed to analyze documentation: ${res.status}`);\n  }\n\n  return {\n    suggestions: result.suggestions ?? [],\n    warnings: result.warnings ?? [],\n    sampleSnippet: result.sampleSnippet,\n  };\n}\n\nexport async function createApiSource(data: CreateApiSourceDto): Promise<ApiSource> {
  const res = await fetch(`${API_BASE}/api/sources`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  
  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: "Failed to create source" }));
    throw new Error(error.error || `Failed to create API source: ${res.status}`);
  }
  
  return res.json();
}

export async function updateApiSource(id: string, data: Partial<CreateApiSourceDto>): Promise<ApiSource> {
  const res = await fetch(`${API_BASE}/api/sources/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  
  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: "Failed to update source" }));
    throw new Error(error.error || `Failed to update API source: ${res.status}`);
  }
  
  return res.json();
}

export async function deleteApiSource(id: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/sources/${id}`, {
    method: "DELETE"
  });
  
  if (!res.ok) {
    throw new Error(`Failed to delete API source: ${res.status}`);
  }
}

export async function testApiConnection(id: string): Promise<{ success: boolean; message: string }> {
  const res = await fetch(`${API_BASE}/api/sources/${id}/test-connection`, {
    method: "POST",
    signal: AbortSignal.timeout(15000)
  });
  
  const result = await res.json().catch(() => ({ success: false, message: "Unable to parse response" }));
  
  if (!res.ok) {
    throw new Error(result.message || `Test failed: ${res.status}`);
  }
  
  return result;
}

export async function triggerApiIngestion(id: string): Promise<{
  success: boolean;
  logId: string;
  recordsFetched: number;
  documentsCreated: number;
  documentsFailed: number;
  executionTimeMs: number;
  errorMessage?: string;
}> {
  const res = await fetch(`${API_BASE}/api/sources/${id}/trigger`, {
    method: "POST",
    signal: AbortSignal.timeout(30000)
  });
  
  const result = await res.json().catch(() => ({ success: false, logId: "", recordsFetched: 0, documentsCreated: 0, documentsFailed: 0, executionTimeMs: 0 }));
  
  if (!res.ok) {
    throw new Error(result.errorMessage || `Ingestion failed: ${res.status}`);
  }
  
  return result;
}

export async function getApiIngestionLogs(sourceId: string, limit = 10): Promise<ApiIngestionLog[]> {
  const url = new URL(`${API_BASE}/api/sources/${sourceId}/logs`);
  url.searchParams.set("limit", String(limit));
  
  const res = await fetch(url, { cache: "no-store" });
  
  if (!res.ok) {
    throw new Error(`Failed to get logs: ${res.status}`);
  }
  
  return res.json();
}

