"use client";

import { useEffect, useMemo, useState } from "react";
import { getStagingDocuments, StagingDocumentSummary } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

const STATUS_FILTERS = ["all", "pending", "processing", "done", "failed"] as const;

type StatusFilter = (typeof STATUS_FILTERS)[number];

export function StagingOverview() {
  const [documents, setDocuments] = useState<StagingDocumentSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeFilter, setActiveFilter] = useState<StatusFilter>("processing");

  async function loadDocuments(filter: StatusFilter) {
    setLoading(true);
    setError(null);
    try {
      const data = await getStagingDocuments(filter === "all" ? undefined : filter);
      setDocuments(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDocuments(activeFilter);
    const interval = setInterval(() => loadDocuments(activeFilter), 20000);
    return () => clearInterval(interval);
  }, [activeFilter]);

  const groupedByStatus = useMemo(() => {
    return documents.reduce<Record<string, number>>((acc, doc) => {
      acc[doc.status] = (acc[doc.status] ?? 0) + 1;
      return acc;
    }, {});
  }, [documents]);

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Staging Queue</CardTitle>
        <CardDescription>Latest 50 jobs flowing through the pipeline.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          {STATUS_FILTERS.map((status) => (
            <Button
              key={status}
              type="button"
              variant={status === activeFilter ? "default" : "outline"}
              size="sm"
              onClick={() => setActiveFilter(status)}
              className="capitalize"
            >
              {status}
              {status !== "all" && (
                <Badge variant="secondary" className="ml-2">
                  {groupedByStatus[status] ?? 0}
                </Badge>
              )}
            </Button>
          ))}
        </div>

        {error && <Badge variant="destructive">{error}</Badge>}

        {loading && documents.length === 0 ? (
          <div className="space-y-2">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : documents.length === 0 ? (
          <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
            No jobs in this state right now.
          </div>
        ) : (
          <ScrollArea className="h-64">
            <div className="grid gap-2">
              {documents.map((doc) => (
                <div
                  key={doc.id}
                  className="flex flex-col gap-1 rounded-md border bg-card/70 p-3 text-sm shadow-sm md:flex-row md:items-center md:justify-between"
                >
                  <div className="space-y-1">
                    <div className="font-medium">{doc.sourceUri}</div>
                    <div className="text-xs text-muted-foreground">
                      Job: {doc.jobId}
                      {doc.mime ? ` • ${doc.mime}` : ""}
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <Badge variant="outline" className="capitalize">
                      {doc.status}
                    </Badge>
                    <span className="text-xs text-muted-foreground">Attempts: {doc.attempts}</span>
                    <span className="text-xs text-muted-foreground">
                      Updated {new Date(doc.updatedAt).toLocaleString()}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  );
}
