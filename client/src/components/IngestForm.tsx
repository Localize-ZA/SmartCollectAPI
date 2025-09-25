"use client";
import { useState } from "react";
import { uploadDocument, IngestResponse } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

export function IngestForm() {
  const [file, setFile] = useState<File | null>(null);
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<IngestResponse | null>(null);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setResult(null);
    if (!file) {
      setError("Please select a file to upload.");
      return;
    }
    setLoading(true);
    try {
      const res = await uploadDocument(file, email || undefined);
      setResult(res);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Ingest a Document</CardTitle>
        <CardDescription>Upload JSON, XML, or CSV to be normalized by the server.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="file">File</Label>
            <Input id="file" type="file" onChange={(e) => setFile(e.target.files?.[0] || null)} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="email">Notify Email (optional)</Label>
            <Input id="email" type="email" placeholder="you@example.com" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="flex items-center gap-2">
            <Button type="submit" disabled={loading}>{loading ? "Uploading…" : "Upload"}</Button>
          </div>
        </form>

        {error && (
          <Alert variant="destructive" className="mt-4">
            <AlertTitle>Upload failed</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {result && (
          <Alert className="mt-4">
            <AlertTitle>Accepted</AlertTitle>
            <AlertDescription className="space-y-1">
              <div><span className="font-mono text-xs">job_id</span>: {result.job_id}</div>
              <div><span className="font-mono text-xs">sha256</span>: {result.sha256}</div>
              <div><span className="font-mono text-xs">source_uri</span>: {result.source_uri}</div>
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
