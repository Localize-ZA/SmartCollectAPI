export const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5082";

export async function getHealth(): Promise<{ status: string; ts: string }> {
  const res = await fetch(`${API_BASE}/health`, { cache: "no-store" });
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
