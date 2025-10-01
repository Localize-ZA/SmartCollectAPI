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
import { 
  X, 
  Upload, 
  CheckCircle, 
  XCircle, 
  Clock, 
  FileText, 
  Zap,
  Loader2,
  AlertCircle,
  Mail
} from "lucide-react";

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
    <Card className="w-full hover-lift glass-effect ring-1 ring-border/50">
      <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
            <Upload className="h-5 w-5" />
          </div>
          <div>
            <CardTitle className="text-lg">Ingest Documents</CardTitle>
            <CardDescription>Upload multiple documents including JSON, XML, CSV, TXT, PDF, and Word files to be processed by the ML pipeline</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="pt-6">
        <form onSubmit={onSubmit} className="space-y-6">
          {/* File Selection */}
          <div className="space-y-2">
            <Label htmlFor="files" className="text-sm font-semibold flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Select Files
            </Label>
            <div className="relative">
              <Input 
                id="files" 
                type="file" 
                multiple 
                accept=".json,.xml,.csv,.txt,.pdf,.doc,.docx,.md"
                onChange={handleFileSelect}
                disabled={loading}
                className="file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:text-sm file:font-semibold file:bg-primary file:text-primary-foreground hover:file:bg-primary/90 file:cursor-pointer cursor-pointer"
              />
            </div>
            <p className="text-xs text-muted-foreground">
              Supported formats: JSON, XML, CSV, TXT, PDF, DOC, DOCX, MD
            </p>
          </div>
          
          {/* Selected Files */}
          {files.length > 0 && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <Label className="text-sm font-semibold">Selected Files ({files.length})</Label>
                <Button 
                  type="button" 
                  variant="outline" 
                  size="sm"
                  onClick={() => setFiles([])}
                  disabled={loading}
                  className="h-8"
                >
                  <X className="h-3.5 w-3.5 mr-1.5" />
                  Clear All
                </Button>
              </div>
              
              {loading && (
                <div className="rounded-xl bg-gradient-to-r from-primary/10 to-primary/5 p-4 ring-1 ring-primary/20">
                  <div className="flex items-center justify-between text-sm font-medium mb-2">
                    <div className="flex items-center gap-2">
                      <Loader2 className="h-4 w-4 animate-spin text-primary" />
                      <span>Upload Progress</span>
                    </div>
                    <span className="text-primary">{Math.round(uploadProgress)}%</span>
                  </div>
                  <Progress value={uploadProgress} className="h-2" />
                </div>
              )}
              
              <div className="space-y-2 max-h-80 overflow-y-auto scrollbar-custom pr-2">
                {files.map((fileStatus, index) => {
                  const statusConfig = {
                    pending: { icon: Clock, color: 'text-muted-foreground', bgColor: 'bg-muted/30', ringColor: 'ring-muted/20' },
                    uploading: { icon: Loader2, color: 'text-primary', bgColor: 'bg-primary/10', ringColor: 'ring-primary/20' },
                    success: { icon: CheckCircle, color: 'text-success', bgColor: 'bg-success/10', ringColor: 'ring-success/20' },
                    error: { icon: XCircle, color: 'text-destructive', bgColor: 'bg-destructive/10', ringColor: 'ring-destructive/20' }
                  };
                  
                  const config = statusConfig[fileStatus.status];
                  const StatusIcon = config.icon;
                  
                  return (
                    <div 
                      key={index} 
                      className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-3 hover-lift transition-all"
                    >
                      <div className="flex items-start gap-3">
                        <div className={`flex h-9 w-9 items-center justify-center rounded-lg flex-shrink-0 ${config.bgColor} ${config.color} ring-1 ${config.ringColor}`}>
                          <StatusIcon className={`h-4 w-4 ${fileStatus.status === 'uploading' ? 'animate-spin' : ''}`} />
                        </div>
                        
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-semibold truncate">{fileStatus.file.name}</p>
                          <div className="flex items-center gap-2 mt-1">
                            <p className="text-xs text-muted-foreground">
                              {(fileStatus.file.size / 1024).toFixed(1)} KB
                            </p>
                            <Badge 
                              variant="outline" 
                              className={`text-[10px] px-2 py-0 ${config.color} ${config.bgColor} border-0 ring-1 ${config.ringColor}`}
                            >
                              {fileStatus.status}
                            </Badge>
                          </div>
                          
                          {fileStatus.error && (
                            <div className="mt-2 rounded-lg bg-destructive/10 p-2 ring-1 ring-destructive/20">
                              <p className="text-xs text-destructive font-medium">{fileStatus.error}</p>
                            </div>
                          )}
                          
                          {fileStatus.result && (
                            <div className="mt-2 rounded-lg bg-success/10 p-2 ring-1 ring-success/20">
                              <p className="text-xs text-success font-mono">
                                ✓ Job ID: {fileStatus.result.job_id}
                              </p>
                            </div>
                          )}
                        </div>
                        
                        {!loading && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => removeFile(index)}
                            className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity"
                          >
                            <X className="h-3.5 w-3.5" />
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
          
          {/* Email Notification */}
          <div className="space-y-2">
            <Label htmlFor="email" className="text-sm font-semibold flex items-center gap-2">
              <Mail className="h-4 w-4" />
              Email Notification (optional)
            </Label>
            <Input 
              id="email" 
              type="email" 
              placeholder="you@example.com" 
              value={email} 
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              className="h-10"
            />
            <p className="text-xs text-muted-foreground">
              Get notified when processing completes
            </p>
          </div>
          
          {/* Upload Options */}
          <div className="rounded-xl bg-muted/30 p-4 ring-1 ring-border/50 space-y-3">
            <Label className="text-sm font-semibold flex items-center gap-2">
              <Zap className="h-4 w-4" />
              Upload Options
            </Label>
            <div className="flex items-start space-x-3">
              <input
                type="checkbox"
                id="bulkUpload"
                checked={useBulkUpload}
                onChange={(e) => setUseBulkUpload(e.target.checked)}
                disabled={loading}
                className="mt-1 rounded border-gray-300"
              />
              <div>
                <Label htmlFor="bulkUpload" className="text-sm font-medium cursor-pointer">
                  Use bulk upload mode
                </Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Faster upload with less granular progress tracking
                </p>
              </div>
            </div>
          </div>
          
          {/* Submit Button */}
          <div className="flex items-center gap-3">
            <Button 
              type="submit" 
              disabled={loading || files.length === 0}
              className="flex-1 h-11 text-base font-semibold shadow-lg"
            >
              {loading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Uploading…
                </>
              ) : (
                <>
                  <Upload className="h-4 w-4 mr-2" />
                  Upload {files.length} File{files.length !== 1 ? 's' : ''}
                </>
              )}
            </Button>
          </div>
        </form>

        {/* Error Alert */}
        {error && (
          <div className="mt-6 rounded-xl bg-destructive/10 p-4 ring-1 ring-destructive/20">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-destructive flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <h4 className="text-sm font-semibold text-destructive mb-1">Upload Failed</h4>
                <p className="text-sm text-destructive/90">{error}</p>
              </div>
            </div>
          </div>
        )}

        {/* Success Summary */}
        {multiResult && (
          <div className="mt-6 rounded-xl bg-gradient-to-br from-success/10 to-success/5 p-4 ring-1 ring-success/20">
            <div className="flex items-start gap-3">
              <CheckCircle className="h-5 w-5 text-success flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <h4 className="text-sm font-semibold text-success mb-2">Upload Complete</h4>
                <div className="flex items-center gap-4 mb-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-4 w-4 text-success" />
                    <span className="text-sm font-medium text-success">{multiResult.successCount} successful</span>
                  </div>
                  {multiResult.errorCount > 0 && (
                    <div className="flex items-center gap-2">
                      <XCircle className="h-4 w-4 text-destructive" />
                      <span className="text-sm font-medium text-destructive">{multiResult.errorCount} failed</span>
                    </div>
                  )}
                </div>
                {multiResult.results.length > 0 && (
                  <div>
                    <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">
                      Job IDs:
                    </p>
                    <div className="space-y-1.5 max-h-32 overflow-y-auto scrollbar-custom">
                      {multiResult.results.map((result, i) => (
                        <div key={i} className="text-xs font-mono bg-card/50 px-3 py-2 rounded-lg ring-1 ring-border/30">
                          {result.job_id}
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
