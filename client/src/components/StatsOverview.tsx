"use client";

import { useEffect, useMemo, useState } from "react";
import { getProcessingStats, ProcessingStats } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RefreshCw } from "lucide-react";

export function StatsOverview() {
  const [stats, setStats] = useState<ProcessingStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadStats() {
    try {
      setLoading(true);
      setError(null);
      const data = await getProcessingStats();
      setStats(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadStats();
    const interval = setInterval(loadStats, 30000);
    return () => clearInterval(interval);
  }, []);

  const metrics = useMemo(() => {
    return [
      {
        label: "Total documents",
        value: stats?.totalDocuments ?? 0,
        help: "Number of documents persisted in PostgreSQL",
      },
      {
        label: "With embeddings",
        value: stats?.documentsWithEmbeddings ?? 0,
        help: "Documents that have vector embeddings generated",
      },
      {
        label: "Processed today",
        value: stats?.processedToday ?? 0,
        help: "Documents ingested since midnight UTC",
      },
    ];
  }, [stats]);

  return (
    <Card className="w-full">
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <div>
          <CardTitle>Pipeline Overview</CardTitle>
          <CardDescription>Key metrics collected from the backend services.</CardDescription>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={loadStats}
          disabled={loading}
          title="Refresh metrics"
        >
          <RefreshCw className={`mr-2 size-4 ${loading ? "animate-spin" : ""}`} />
          Refresh
        </Button>
      </CardHeader>
      <CardContent className="space-y-6">
        {error && (
          <Badge variant="destructive" className="text-sm">
            {error}
          </Badge>
        )}
        <div className="grid gap-4 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4">
          {metrics.map((metric) => (
            <div key={metric.label} className="rounded-lg border bg-card p-4 shadow-sm">
              <div className="text-sm text-muted-foreground">{metric.label}</div>
              {loading && !stats ? (
                <Skeleton className="mt-2 h-8 w-20" />
              ) : (
                <div className="mt-2 text-xl font-semibold">{metric.value}</div>
              )}
              <div className="mt-1 text-xs text-muted-foreground">{metric.help}</div>
            </div>
          ))}
        </div>
        <div className="space-y-3">
          <div className="text-sm font-medium">Staging queue</div>
          {loading && !stats ? (
            <Skeleton className="h-6 w-48" />
          ) : stats && Object.keys(stats.stagingStatus ?? {}).length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {Object.entries(stats.stagingStatus)
                .sort((a, b) => b[1] - a[1])
                .map(([status, count]) => (
                  <Badge key={status} variant="outline" className="px-3 py-1 capitalize">
                    {status}: {count}
                  </Badge>
                ))}
            </div>
          ) : (
            <div className="text-sm text-muted-foreground">No staging activity recorded yet.</div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
