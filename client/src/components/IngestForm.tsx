"use client";
import { useState } from "react";
import { uploadDocument, IngestResponse, getErrorMessage, getErrorTitle } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Progress } from "@/components/ui/progress";

export function IngestForm() {
  const [file, setFile] = useState<File | null>(null);
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown | null>(null);
  const [result, setResult] = useState<IngestResponse | null>(null);

  function validateFile(file: File): string | null {
    // Maximum file size: 100MB
    const maxSize = 100 * 1024 * 1024;
    if (file.size > maxSize) {
      return `File size (${(file.size / (1024 * 1024)).toFixed(1)}MB) exceeds maximum allowed size of 100MB`;
    }

    // Check file type
    const allowedTypes = [
      'application/json',
      'text/plain',
      'application/xml',
      'text/xml',
      'text/csv',
      'application/pdf'
    ];
    
    if (!allowedTypes.includes(file.type)) {
      return `File type '${file.type || 'unknown'}' is not supported. Please upload JSON, XML, CSV, or PDF files.`;
    }

    return null;
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setResult(null);
    
    if (!file) {
      setError(new Error("Please select a file to upload."));
      return;
    }

    // Validate file
    const validationError = validateFile(file);
    if (validationError) {
      setError(new Error(validationError));
      return;
    }

    // Validate email if provided
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError(new Error("Please enter a valid email address."));
      return;
    }

    setLoading(true);
    try {
      const res = await uploadDocument(file, email || undefined);
      setResult(res);
      // Reset form on success
      setFile(null);
      setEmail("");
      // Reset file input
      const fileInput = document.getElementById('file') as HTMLInputElement;
      if (fileInput) fileInput.value = '';
    } catch (e: unknown) {
      setError(e);
    } finally {
      setLoading(false);
    }
  }

  function handleRetry() {
    setError(null);
    if (file) {
      onSubmit(new Event('submit') as any);
    }
  }

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Ingest a Document</CardTitle>
        <CardDescription>
          Upload JSON, XML, CSV, or PDF files to be processed by the server. 
          Maximum file size: 100MB.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="file">File</Label>
            <Input 
              id="file" 
              type="file" 
              accept=".json,.xml,.csv,.pdf,text/plain,application/json,application/xml,text/xml,text/csv,application/pdf"
              onChange={(e) => setFile(e.target.files?.[0] || null)} 
              disabled={loading}
            />
            {file && (
              <div className="text-sm text-muted-foreground">
                Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)
              </div>
            )}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="email">Notify Email (optional)</Label>
            <Input 
              id="email" 
              type="email" 
              placeholder="you@example.com" 
              value={email} 
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
            />
          </div>
          <div className="flex items-center gap-2">
            <Button type="submit" disabled={loading || !file}>
              {loading ? "Uploading…" : "Upload"}
            </Button>
            {error && (
              <Button type="button" variant="outline" onClick={handleRetry} disabled={loading}>
                Retry
              </Button>
            )}
          </div>
        </form>

        {loading && (
          <div className="mt-4 space-y-2">
            <div className="text-sm text-muted-foreground">Uploading file...</div>
            <Progress value={undefined} className="w-full" />
          </div>
        )}

        {error && (
          <Alert variant="destructive" className="mt-4">
            <AlertTitle>{getErrorTitle(error)}</AlertTitle>
            <AlertDescription>{getErrorMessage(error)}</AlertDescription>
          </Alert>
        )}

        {result && (
          <Alert className="mt-4">
            <AlertTitle>Upload Successful</AlertTitle>
            <AlertDescription className="space-y-2">
              <p>Your document has been accepted for processing.</p>
              <div className="space-y-1 text-sm font-mono">
                <div><span className="font-sans font-medium">Job ID:</span> {result.job_id}</div>
                <div><span className="font-sans font-medium">SHA256:</span> {result.sha256}</div>
                <div><span className="font-sans font-medium">Source:</span> {result.source_uri}</div>
              </div>
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
