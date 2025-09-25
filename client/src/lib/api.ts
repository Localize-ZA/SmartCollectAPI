export const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5082";

export interface ApiError {
  error: string;
  details?: string;
  timestamp?: string;
  requestId?: string;
}

export class ApiException extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public apiError?: ApiError,
  ) {
    super(apiError?.error || `HTTP ${status}: ${statusText}`);
    this.name = 'ApiException';
  }
}

async function handleApiResponse<T>(response: Response): Promise<T> {
  if (response.ok) {
    return response.json();
  }

  let apiError: ApiError | undefined;
  try {
    const errorData = await response.json();
    if (errorData.error) {
      apiError = errorData as ApiError;
    }
  } catch {
    // Response is not JSON or malformed
  }

  throw new ApiException(response.status, response.statusText, apiError);
}

export async function getHealth(): Promise<{ status: string; ts: string }> {
  try {
    const res = await fetch(`${API_BASE}/health`, { 
      cache: "no-store",
      signal: AbortSignal.timeout(10000) // 10 second timeout
    });
    return handleApiResponse(res);
  } catch (error) {
    if (error instanceof ApiException) {
      throw error;
    }
    if (error instanceof DOMException && error.name === 'TimeoutError') {
      throw new Error('Health check timed out');
    }
    if (error instanceof TypeError) {
      throw new Error('Unable to connect to server');
    }
    throw new Error(`Health check failed: ${error instanceof Error ? error.message : String(error)}`);
  }
}

export interface IngestResponse {
  job_id: string;
  sha256: string;
  source_uri: string;
}

export async function uploadDocument(file: File, notifyEmail?: string): Promise<IngestResponse> {
  try {
    const form = new FormData();
    form.append("file", file);
    if (notifyEmail) form.append("notify_email", notifyEmail);
    
    const res = await fetch(`${API_BASE}/api/ingest`, {
      method: "POST",
      body: form,
      signal: AbortSignal.timeout(120000) // 2 minute timeout for uploads
    });
    
    if (res.status === 202) {
      // Accepted response is success for async processing
      return res.json();
    }
    
    return handleApiResponse(res);
  } catch (error) {
    if (error instanceof ApiException) {
      throw error;
    }
    if (error instanceof DOMException && error.name === 'TimeoutError') {
      throw new Error('Upload timed out - the file may be too large or the connection is slow');
    }
    if (error instanceof TypeError) {
      throw new Error('Unable to connect to server');
    }
    throw new Error(`Upload failed: ${error instanceof Error ? error.message : String(error)}`);
  }
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiException) {
    return error.apiError?.details || error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return String(error);
}

export function getErrorTitle(error: unknown): string {
  if (error instanceof ApiException) {
    return error.apiError?.error || 'Request Failed';
  }
  return 'Error';
}
