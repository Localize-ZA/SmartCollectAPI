"use client";
import { useState } from "react";
import { uploadDocument, uploadMultipleDocuments, uploadMultipleDocumentsBulk, IngestResponse, MultiIngestResponse } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { X, Upload, CheckCircle, XCircle, Clock } from "lucide-react";

interface FileStatus {
  file: File;
  status: 'pending' | 'uploading' | 'success' | 'error';
  result?: IngestResponse;
  error?: string;
}

export function IngestForm() {
  const [files, setFiles] = useState<FileStatus[]>([]);
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [multiResult, setMultiResult] = useState<MultiIngestResponse | null>(null);
  const [useBulkUpload, setUseBulkUpload] = useState(true);

  function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const selectedFiles = Array.from(e.target.files || []);
    const newFiles: FileStatus[] = selectedFiles.map(file => ({
      file,
      status: 'pending'
    }));
    setFiles(prev => [...prev, ...newFiles]);
  }

  function removeFile(index: number) {
    setFiles(prev => prev.filter((_, i) => i !== index));
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setMultiResult(null);
    
    if (files.length === 0) {
      setError("Please select at least one file to upload.");
      return;
    }

    setLoading(true);
    
    try {
      if (useBulkUpload) {
        // Use bulk upload - faster but no individual progress
        setFiles(prev => prev.map(f => ({ ...f, status: 'uploading' as const })));
        
        const result = await uploadMultipleDocumentsBulk(
          files.map(f => f.file),
          email || undefined
        );
        
        // Update file statuses based on results
        setFiles(prev => prev.map(f => {
          const successResult = result.results.find((r: any) => r.fileName === f.file.name);
          const errorResult = result.errors.find((e: any) => e.fileName === f.file.name);
          
          if (successResult) {
            return { 
              ...f, 
              status: 'success' as const, 
              result: { 
                job_id: successResult.job_id, 
                sha256: successResult.sha256, 
                source_uri: successResult.source_uri 
              } 
            };
          } else if (errorResult) {
            return { ...f, status: 'error' as const, error: errorResult.error };
          }
          return { ...f, status: 'success' as const };
        }));
        
        setMultiResult(result);
      } else {
        // Use concurrent upload with individual progress
        setFiles(prev => prev.map(f => ({ ...f, status: 'pending' as const })));
        
        const result = await uploadMultipleDocuments(
          files.map(f => f.file),
          email || undefined,
          (progress) => {
            setFiles(prev => prev.map(f => 
              f.file.name === progress.fileName 
                ? { ...f, status: progress.status, result: progress.result, error: progress.error }
                : f
            ));
          }
        );
        
        setMultiResult(result);
      }
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }

  const getStatusIcon = (status: FileStatus['status']) => {
    switch (status) {
      case 'pending': return <Clock className="h-4 w-4 text-muted-foreground" />;
      case 'uploading': return <Upload className="h-4 w-4 text-blue-500 animate-spin" />;
      case 'success': return <CheckCircle className="h-4 w-4 text-green-500" />;
      case 'error': return <XCircle className="h-4 w-4 text-red-500" />;
    }
  };

  const getStatusVariant = (status: FileStatus['status']) => {
    switch (status) {
      case 'pending': return 'secondary' as const;
      case 'uploading': return 'default' as const;
      case 'success': return 'default' as const;
      case 'error': return 'destructive' as const;
    }
  };

  const uploadProgress = files.length > 0 ? 
    (files.filter(f => f.status === 'success').length / files.length) * 100 : 0;

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Ingest Documents</CardTitle>
        <CardDescription>Upload multiple JSON, XML, or CSV files to be normalized by the server.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="files">Files</Label>
            <Input 
              id="files" 
              type="file" 
              multiple 
              accept=".json,.xml,.csv,.txt"
              onChange={handleFileSelect} 
            />
          </div>
          
          {files.length > 0 && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label>Selected Files ({files.length})</Label>
                <Button 
                  type="button" 
                  variant="outline" 
                  size="sm"
                  onClick={() => setFiles([])}
                  disabled={loading}
                >
                  Clear All
                </Button>
              </div>
              
              {loading && (
                <div className="space-y-2">
                  <div className="flex items-center justify-between text-sm">
                    <span>Upload Progress</span>
                    <span>{Math.round(uploadProgress)}%</span>
                  </div>
                  <Progress value={uploadProgress} className="w-full" />
                </div>
              )}
              
              <div className="space-y-2 max-h-60 overflow-y-auto">
                {files.map((fileStatus, index) => (
                  <div key={index} className="flex items-center justify-between p-3 border rounded-lg">
                    <div className="flex items-center gap-3 flex-1 min-w-0">
                      {getStatusIcon(fileStatus.status)}
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium truncate">{fileStatus.file.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {(fileStatus.file.size / 1024).toFixed(1)} KB
                        </p>
                        {fileStatus.error && (
                          <p className="text-xs text-red-500 mt-1">{fileStatus.error}</p>
                        )}
                        {fileStatus.result && (
                          <p className="text-xs text-green-600 mt-1 font-mono">
                            Job ID: {fileStatus.result.job_id}
                          </p>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant={getStatusVariant(fileStatus.status)}>
                        {fileStatus.status}
                      </Badge>
                      {!loading && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => removeFile(index)}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
          
          <div className="grid gap-2">
            <Label htmlFor="email">Notify Email (optional)</Label>
            <Input 
              id="email" 
              type="email" 
              placeholder="you@example.com" 
              value={email} 
              onChange={(e) => setEmail(e.target.value)} 
            />
          </div>
          
          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              id="bulkUpload"
              checked={useBulkUpload}
              onChange={(e) => setUseBulkUpload(e.target.checked)}
              className="rounded border-gray-300"
            />
            <Label htmlFor="bulkUpload" className="text-sm">
              Use bulk upload (faster, less granular progress)
            </Label>
          </div>
          
          <div className="flex items-center gap-2">
            <Button 
              type="submit" 
              disabled={loading || files.length === 0}
            >
              {loading ? "Uploading…" : `Upload ${files.length} File${files.length !== 1 ? 's' : ''}`}
            </Button>
          </div>
        </form>

        {error && (
          <Alert variant="destructive" className="mt-4">
            <AlertTitle>Upload failed</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {multiResult && (
          <Alert className="mt-4">
            <AlertTitle>Upload Complete</AlertTitle>
            <AlertDescription className="space-y-2">
              <div className="flex gap-4 text-sm">
                <span className="text-green-600">✓ {multiResult.successCount} successful</span>
                {multiResult.errorCount > 0 && (
                  <span className="text-red-600">✗ {multiResult.errorCount} failed</span>
                )}
              </div>
              {multiResult.results.length > 0 && (
                <div className="mt-2">
                  <p className="text-sm font-medium mb-1">Successful uploads:</p>
                  <div className="space-y-1 max-h-40 overflow-y-auto">
                    {multiResult.results.map((result, i) => (
                      <div key={i} className="text-xs font-mono bg-muted p-2 rounded">
                        Job ID: {result.job_id}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
