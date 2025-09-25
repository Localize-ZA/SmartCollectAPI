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
