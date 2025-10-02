"use client";

import { useEffect, useState } from "react";
import { getDocumentChunks, type DocumentChunk } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { FileText, Hash, Clock } from "lucide-react";

interface DocumentChunksViewProps {
  documentId: string;
}

export function DocumentChunksView({ documentId }: DocumentChunksViewProps) {
  const [chunks, setChunks] = useState<DocumentChunk[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!documentId) return;

    const fetchChunks = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await getDocumentChunks(documentId);
        setChunks(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load chunks");
      } finally {
        setLoading(false);
      }
    };

    void fetchChunks();
  }, [documentId]);

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Document Chunks</CardTitle>
          <CardDescription>Loading chunks...</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Document Chunks</CardTitle>
        </CardHeader>
        <CardContent>
          <Badge variant="destructive">{error}</Badge>
        </CardContent>
      </Card>
    );
  }

  if (chunks.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Document Chunks</CardTitle>
          <CardDescription>This document has not been chunked</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Documents under 2000 characters are not automatically chunked.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileText className="h-5 w-5" />
          Document Chunks
        </CardTitle>
        <CardDescription>
          {chunks.length} chunk{chunks.length !== 1 ? "s" : ""} • Total {chunks.reduce((sum, c) => sum + c.tokenCount, 0).toLocaleString()} tokens
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {chunks.map((chunk, index) => (
          <div
            key={chunk.id}
            className="rounded-lg border p-4 space-y-3 hover:bg-accent/50 transition-colors"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <Badge variant="outline" className="font-mono">
                  Chunk {index + 1}
                </Badge>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <Hash className="h-3 w-3" />
                  <span>{chunk.tokenCount} tokens</span>
                </div>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>Position: {chunk.startPosition}-{chunk.endPosition}</span>
                </div>
              </div>
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Clock className="h-3 w-3" />
                <span>{new Date(chunk.createdAt).toLocaleDateString()}</span>
              </div>
            </div>
            
            <div className="prose prose-sm dark:prose-invert max-w-none">
              <p className="text-sm leading-relaxed whitespace-pre-wrap">
                {chunk.content}
              </p>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
