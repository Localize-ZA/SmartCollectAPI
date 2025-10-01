"use client";

import { useEffect, useMemo, useState, useTransition } from "react";
import { getStagingDocuments, StagingDocumentSummary } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { 
  Clock, 
  CheckCircle2, 
  XCircle, 
  Loader2, 
  AlertCircle, 
  FileText,
  Activity,
  Layers
} from "lucide-react";

const STATUS_FILTERS = ["all", "pending", "processing", "done", "failed"] as const;

type StatusFilter = (typeof STATUS_FILTERS)[number];

export function StagingOverview() {
  const [documents, setDocuments] = useState<StagingDocumentSummary[]>([]);
  const [allDocuments, setAllDocuments] = useState<StagingDocumentSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeFilter, setActiveFilter] = useState<StatusFilter>("processing");
  const [isPending, startTransition] = useTransition();

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  useEffect(() => {
    const initial = searchParams.get("staging");
    if (initial && STATUS_FILTERS.includes(initial as StatusFilter)) {
      if (initial !== activeFilter) {
        setActiveFilter(initial as StatusFilter);
      }
      return;
    }

    if (!initial && activeFilter !== "processing") {
      setActiveFilter("processing");
    }
  }, [searchParams, activeFilter]);

  // Fetch all documents for status counts
  useEffect(() => {
    let cancelled = false;

    const fetchAllDocs = async () => {
      if (cancelled) return;
      try {
        const allData = await getStagingDocuments(); // Get all documents for counts
        if (!cancelled) {
          setAllDocuments(allData);
        }
      } catch (err: unknown) {
        // Don't show error for background fetch
        console.warn('Failed to fetch all staging documents:', err);
      }
    };

    fetchAllDocs();
    const interval = setInterval(fetchAllDocs, 20000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  // Fetch filtered documents for display
  useEffect(() => {
    let cancelled = false;

    const fetchFilteredDocs = async () => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
      try {
        const data = await getStagingDocuments(activeFilter === "all" ? undefined : activeFilter);
        if (!cancelled) {
          setDocuments(data);
        }
      } catch (err: unknown) {
        if (!cancelled) {
          const message = err instanceof Error ? err.message : String(err);
          setError(message);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    fetchFilteredDocs();
    return () => {
      cancelled = true;
    };
  }, [activeFilter]);

  function handleFilterChange(status: StatusFilter) {
    if (status === activeFilter) return;
    
    // Update the filter immediately for responsive UI
    setActiveFilter(status);
    
    // Update URL in a transition to avoid blocking
    startTransition(() => {
      const params = new URLSearchParams(searchParams.toString());
      if (status === "processing") {
        params.delete("staging");
      } else {
        params.set("staging", status);
      }
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    });
  }

  const groupedByStatus = useMemo(() => {
    return allDocuments.reduce<Record<string, number>>((acc, doc) => {
      acc[doc.status] = (acc[doc.status] ?? 0) + 1;
      return acc;
    }, {});
  }, [allDocuments]);

  const statusConfig = {
    pending: { icon: Clock, color: "text-warning", bgColor: "bg-warning/10", ringColor: "ring-warning/20" },
    processing: { icon: Loader2, color: "text-primary", bgColor: "bg-primary/10", ringColor: "ring-primary/20" },
    done: { icon: CheckCircle2, color: "text-success", bgColor: "bg-success/10", ringColor: "ring-success/20" },
    failed: { icon: XCircle, color: "text-destructive", bgColor: "bg-destructive/10", ringColor: "ring-destructive/20" },
  };

  return (
    <Card className="w-full hover-lift glass-effect ring-1 ring-border/50">
      <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
            <Layers className="h-5 w-5" />
          </div>
          <div>
            <CardTitle className="text-lg">Staging Queue</CardTitle>
            <CardDescription>Latest 50 jobs flowing through the pipeline</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-6 pt-6">
        {/* Status Summary Cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50 hover-lift">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide">Pending</p>
                <p className="text-2xl font-bold text-warning mt-1">
                  {allDocuments.length === 0 ? "..." : (groupedByStatus.pending ?? 0)}
                </p>
              </div>
              <Clock className="h-8 w-8 text-warning opacity-50" />
            </div>
          </div>
          
          <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50 hover-lift">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide">Processing</p>
                <p className="text-2xl font-bold text-primary mt-1">
                  {allDocuments.length === 0 ? "..." : (groupedByStatus.processing ?? 0)}
                </p>
              </div>
              <Loader2 className="h-8 w-8 text-primary opacity-50 animate-spin" />
            </div>
          </div>
          
          <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50 hover-lift">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide">Completed</p>
                <p className="text-2xl font-bold text-success mt-1">
                  {allDocuments.length === 0 ? "..." : (groupedByStatus.done ?? 0)}
                </p>
              </div>
              <CheckCircle2 className="h-8 w-8 text-success opacity-50" />
            </div>
          </div>
          
          <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50 hover-lift">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide">Failed</p>
                <p className="text-2xl font-bold text-destructive mt-1">
                  {allDocuments.length === 0 ? "..." : (groupedByStatus.failed ?? 0)}
                </p>
              </div>
              <XCircle className="h-8 w-8 text-destructive opacity-50" />
            </div>
          </div>
        </div>

        {/* Filter Buttons */}
        <div className="flex flex-wrap gap-2">
          {STATUS_FILTERS.map((status) => {
            const isActive = status === activeFilter;
            const Icon = status === "all" ? Activity : 
                        status === "pending" ? Clock :
                        status === "processing" ? Loader2 :
                        status === "done" ? CheckCircle2 : XCircle;
            
            return (
              <Button
                key={status}
                type="button"
                variant={isActive ? "default" : "outline"}
                size="sm"
                onClick={() => handleFilterChange(status)}
                className={`capitalize transition-all ${
                  isActive ? 'shadow-lg ring-2 ring-primary/20' : ''
                }`}
                disabled={isPending}
              >
                <Icon className={`h-3.5 w-3.5 mr-2 ${status === "processing" && isActive ? "animate-spin" : ""}`} />
                {status}
                {status !== "all" && (
                  <Badge variant="secondary" className="ml-2 text-[10px] px-1.5 py-0">
                    {allDocuments.length === 0 ? "..." : (groupedByStatus[status] ?? 0)}
                  </Badge>
                )}
              </Button>
            );
          })}
        </div>

        {error && (
          <div className="rounded-lg bg-destructive/10 p-3 ring-1 ring-destructive/20 flex items-center gap-2">
            <AlertCircle className="h-4 w-4 text-destructive flex-shrink-0" />
            <span className="text-sm font-medium text-destructive">{error}</span>
          </div>
        )}

        {loading && documents.length === 0 ? (
          <div className="space-y-3">
            <Skeleton className="h-20 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
          </div>
        ) : documents.length === 0 ? (
          <div className="rounded-xl bg-muted/30 p-8 text-center ring-1 ring-border/50">
            <FileText className="h-12 w-12 mx-auto text-muted-foreground/50 mb-3" />
            <p className="text-sm font-medium text-muted-foreground">No jobs in this state right now</p>
            <p className="text-xs text-muted-foreground/70 mt-1">Jobs will appear here as they're processed</p>
          </div>
        ) : (
          <ScrollArea className="h-80 scrollbar-custom">
            <div className="grid gap-3 pr-4">
              {documents.map((doc) => {
                const StatusIcon = statusConfig[doc.status as keyof typeof statusConfig]?.icon || AlertCircle;
                const statusColor = statusConfig[doc.status as keyof typeof statusConfig]?.color || "text-muted-foreground";
                const statusBgColor = statusConfig[doc.status as keyof typeof statusConfig]?.bgColor || "bg-muted/10";
                const statusRingColor = statusConfig[doc.status as keyof typeof statusConfig]?.ringColor || "ring-muted/20";
                
                return (
                  <div
                    key={doc.id}
                    className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
                  >
                    <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                      <div className="flex items-start gap-3 flex-1 min-w-0">
                        <div className={`flex h-10 w-10 items-center justify-center rounded-lg flex-shrink-0 ${statusBgColor} ${statusColor} ring-1 ${statusRingColor}`}>
                          <StatusIcon className={`h-5 w-5 ${doc.status === "processing" ? "animate-spin" : ""}`} />
                        </div>
                        
                        <div className="flex-1 min-w-0">
                          <div className="font-semibold text-sm truncate mb-1">{doc.sourceUri}</div>
                          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                            <span className="font-mono bg-muted/50 px-2 py-0.5 rounded">
                              Job: {doc.jobId}
                            </span>
                            {doc.mime && (
                              <span className="bg-muted/50 px-2 py-0.5 rounded">
                                {doc.mime}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                      
                      <div className="flex flex-wrap items-center gap-3 md:flex-shrink-0">
                        <Badge 
                          variant="outline" 
                          className={`capitalize text-xs ${statusColor} ${statusBgColor} border-0 ring-1 ${statusRingColor}`}
                        >
                          {doc.status === "processing" && <Loader2 className="h-3 w-3 mr-1 animate-spin" />}
                          {doc.status}
                        </Badge>
                        <div className="flex items-center gap-1 text-xs text-muted-foreground">
                          <Activity className="h-3 w-3" />
                          <span>{doc.attempts} attempt{doc.attempts !== 1 ? "s" : ""}</span>
                        </div>
                        <span className="text-xs text-muted-foreground">
                          {new Date(doc.updatedAt).toLocaleString([], { 
                            month: 'short', 
                            day: 'numeric', 
                            hour: '2-digit', 
                            minute: '2-digit' 
                          })}
                        </span>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  );
}
