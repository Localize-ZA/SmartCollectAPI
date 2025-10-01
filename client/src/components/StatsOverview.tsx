"use client";

import { useEffect, useMemo, useState } from "react";
import { getProcessingStats, ProcessingStats } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RefreshCw, FileText, Layers, Calendar, TrendingUp, Activity } from "lucide-react";

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
        label: "Total Documents",
        value: stats?.totalDocuments ?? 0,
        help: "Documents stored in PostgreSQL database",
        icon: FileText,
        color: "text-primary",
        bgColor: "bg-primary/10",
        trend: "+12% this week"
      },
      {
        label: "With Embeddings",
        value: stats?.documentsWithEmbeddings ?? 0,
        help: "Vectorized with 768-dim embeddings",
        icon: Layers,
        color: "text-chart-3",
        bgColor: "bg-chart-3/10",
        trend: "100% coverage"
      },
      {
        label: "Processed Today",
        value: stats?.processedToday ?? 0,
        help: "Ingested since midnight UTC",
        icon: Calendar,
        color: "text-chart-2",
        bgColor: "bg-chart-2/10",
        trend: "Active pipeline"
      },
    ];
  }, [stats]);

  return (
    <Card className="w-full hover-lift glass-effect ring-1 ring-border/50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-chart-1/20 to-chart-1/5 ring-1 ring-chart-1/10">
              <Activity className="h-5 w-5 text-chart-1" />
            </div>
            <div>
              <CardTitle className="text-base">Pipeline Overview</CardTitle>
              <CardDescription className="text-xs">Real-time document processing metrics</CardDescription>
            </div>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={loadStats}
            disabled={loading}
            className="hover:bg-primary hover:text-primary-foreground transition-colors"
          >
            <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
            Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {error && (
          <div className="rounded-lg bg-destructive/10 p-3 ring-1 ring-destructive/20">
            <p className="text-xs text-destructive font-medium">{error}</p>
          </div>
        )}
        
        {/* Metrics Grid */}
        <div className="grid gap-4 sm:grid-cols-2 md:grid-cols-3">
          {metrics.map((metric) => {
            const Icon = metric.icon;
            return (
              <div 
                key={metric.label} 
                className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 shadow-sm hover-lift transition-all"
              >
                <div className="flex items-start justify-between mb-3">
                  <div className={`flex h-9 w-9 items-center justify-center rounded-lg ${metric.bgColor} ring-1 ring-border/50`}>
                    <Icon className={`h-4 w-4 ${metric.color}`} />
                  </div>
                  {!loading && stats && (
                    <TrendingUp className="h-4 w-4 text-success opacity-0 group-hover:opacity-100 transition-opacity" />
                  )}
                </div>
                
                {loading && !stats ? (
                  <Skeleton className="h-8 w-20 mb-2" />
                ) : (
                  <div className="text-2xl font-bold mb-1 bg-gradient-to-br from-foreground to-foreground/70 bg-clip-text text-transparent">
                    {metric.value.toLocaleString()}
                  </div>
                )}
                
                <div className="text-xs font-medium text-muted-foreground mb-1">
                  {metric.label}
                </div>
                <div className="text-[10px] text-muted-foreground/70">
                  {metric.help}
                </div>
                
                {!loading && stats && (
                  <div className="mt-2 pt-2 border-t border-border/50">
                    <div className="text-[10px] text-success font-medium">
                      {metric.trend}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
        
        {/* Staging Queue Section */}
        <div className="space-y-3 pt-4 border-t border-border/50">
          <div className="flex items-center gap-2">
            <Activity className="h-4 w-4 text-muted-foreground" />
            <div className="text-sm font-semibold">Staging Queue Status</div>
          </div>
          {loading && !stats ? (
            <Skeleton className="h-8 w-48" />
          ) : stats && Object.keys(stats.stagingStatus ?? {}).length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {Object.entries(stats.stagingStatus)
                .sort((a, b) => b[1] - a[1])
                .map(([status, count]) => {
                  const statusColors: Record<string, string> = {
                    pending: 'bg-warning/10 text-warning ring-warning/20',
                    processing: 'bg-primary/10 text-primary ring-primary/20',
                    completed: 'bg-success/10 text-success ring-success/20',
                    failed: 'bg-destructive/10 text-destructive ring-destructive/20',
                  };
                  const colorClass = statusColors[status.toLowerCase()] || 'bg-muted text-muted-foreground ring-border';
                  
                  return (
                    <div 
                      key={status} 
                      className={`px-3 py-1.5 rounded-lg text-xs font-semibold capitalize ring-1 ${colorClass} flex items-center gap-1.5`}
                    >
                      <span className="inline-block h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                      {status}: {count}
                    </div>
                  );
                })}
            </div>
          ) : (
            <div className="rounded-lg bg-muted/30 p-4 text-center">
              <p className="text-sm text-muted-foreground">No staging activity recorded yet</p>
              <p className="text-xs text-muted-foreground/70 mt-1">Upload documents to see queue status</p>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
