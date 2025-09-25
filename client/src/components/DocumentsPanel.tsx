"use client";

import { useEffect, useMemo, useState } from "react";
import {
  DocumentDetail,
  DocumentSummary,
  PagedDocuments,
  getDocument,
  getDocuments,
} from "@/lib/api";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { CopyButton } from "@/components/ui/copy-button";
import { formatDistanceToNow } from "date-fns";
import { Eye } from "lucide-react";

const PAGE_SIZE = 10;

export function DocumentsPanel() {
  const [page, setPage] = useState(1);
  const [paged, setPaged] = useState<PagedDocuments | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [detailId, setDetailId] = useState<string | null>(null);
  const [detail, setDetail] = useState<DocumentDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  async function loadPage(nextPage: number) {
    setLoading(true);
    setError(null);
    try {
      const data = await getDocuments(nextPage, PAGE_SIZE);
      setPaged(data);
      setPage(nextPage);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadPage(1);
  }, []);

  async function openDetail(id: string) {
    setDetailId(id);
    setDetail(null);
    setDetailError(null);
    setDetailLoading(true);
    try {
      const doc = await getDocument(id);
      setDetail(doc);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setDetailError(message);
    } finally {
      setDetailLoading(false);
    }
  }

  const totalPages = paged?.totalPages ?? 1;

  const paginationNumbers = useMemo(() => {
    if (!totalPages) return [];
    const maxButtons = 5;
  const pages: number[] = [];
  let start = Math.max(1, page - Math.floor(maxButtons / 2));
  const end = Math.min(totalPages, start + maxButtons - 1);
    if (end - start + 1 < maxButtons) {
      start = Math.max(1, end - maxButtons + 1);
    }
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }, [page, totalPages]);

  function renderRow(item: DocumentSummary) {
    return (
      <TableRow key={item.id}>
        <TableCell className="max-w-[220px] truncate" title={item.sourceUri}>
          {item.sourceUri}
        </TableCell>
        <TableCell>{item.mime ?? "Unknown"}</TableCell>
        <TableCell className="hidden md:table-cell">
          {item.sha256 ? <span className="font-mono text-xs">{item.sha256.slice(0, 12)}…</span> : "—"}
        </TableCell>
        <TableCell>
          {formatDistanceToNow(new Date(item.createdAt), { addSuffix: true })}
        </TableCell>
        <TableCell>
          <Badge variant={item.hasEmbedding ? "default" : "secondary"}>
            {item.hasEmbedding ? "Embedded" : "Pending"}
          </Badge>
        </TableCell>
        <TableCell align="right">
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={() => openDetail(item.id)}
            title="Inspect document"
          >
            <Eye className="size-4" />
          </Button>
        </TableCell>
      </TableRow>
    );
  }

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Processed Documents</CardTitle>
        <CardDescription>Explore canonical records stored in PostgreSQL + pgvector.</CardDescription>
      </CardHeader>
      <CardContent>
        {error && (
          <Badge variant="destructive" className="mb-4">
            {error}
          </Badge>
        )}

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Source</TableHead>
              <TableHead>Mime</TableHead>
              <TableHead className="hidden md:table-cell">SHA256</TableHead>
              <TableHead>Created</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading && !paged ? (
              <TableRow>
                <TableCell colSpan={6}>
                  <div className="space-y-2 py-6">
                    <Skeleton className="h-6 w-full" />
                    <Skeleton className="h-6 w-full" />
                    <Skeleton className="h-6 w-full" />
                  </div>
                </TableCell>
              </TableRow>
            ) : paged && paged.items.length > 0 ? (
              paged.items.map(renderRow)
            ) : (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-sm text-muted-foreground">
                  No documents processed yet. Upload a file to get started.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </CardContent>
      <CardFooter className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="text-xs text-muted-foreground">
          {paged ? `Showing ${(paged.page - 1) * paged.pageSize + 1}-${Math.min(paged.page * paged.pageSize, paged.totalCount)} of ${paged.totalCount}` : ""}
        </div>
        {paged && paged.totalPages > 1 && (
          <Pagination>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious
                  href="#"
                  aria-disabled={page <= 1}
                  onClick={(e) => {
                    e.preventDefault();
                    if (page > 1) loadPage(page - 1);
                  }}
                />
              </PaginationItem>
              {paginationNumbers.map((num) => (
                <PaginationItem key={num}>
                  <PaginationLink
                    href="#"
                    isActive={num === page}
                    onClick={(e) => {
                      e.preventDefault();
                      if (num !== page) loadPage(num);
                    }}
                  >
                    {num}
                  </PaginationLink>
                </PaginationItem>
              ))}
              <PaginationItem>
                <PaginationNext
                  href="#"
                  aria-disabled={page >= totalPages}
                  onClick={(e) => {
                    e.preventDefault();
                    if (page < totalPages) loadPage(page + 1);
                  }}
                />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        )}
      </CardFooter>

      <Dialog open={detailId !== null} onOpenChange={(open) => !open && setDetailId(null)}>
        <DialogContent className="max-h-[80vh] max-w-3xl overflow-hidden">
          <DialogHeader>
            <DialogTitle>Document details</DialogTitle>
            <DialogDescription>
              {detailId}
              <CopyButton value={detailId ?? ""} className="ml-2" />
            </DialogDescription>
          </DialogHeader>
          {detailLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-6 w-[60%]" />
              <Skeleton className="h-6 w-full" />
              <Skeleton className="h-[200px] w-full" />
            </div>
          ) : detailError ? (
            <Badge variant="destructive">{detailError}</Badge>
          ) : detail ? (
            <ScrollArea className="max-h-[60vh] rounded-md border p-4">
              <div className="space-y-4 text-sm">
                <div className="space-y-1">
                  <div className="font-semibold">Source</div>
                  <div className="text-muted-foreground">{detail.sourceUri}</div>
                </div>
                <div className="grid gap-2 md:grid-cols-2">
                  <InfoRow label="Mime" value={detail.mime ?? "Unknown"} />
                  <InfoRow label="SHA256" value={detail.sha256 ?? "—"} mono />
                  <InfoRow label="Created" value={new Date(detail.createdAt).toLocaleString()} />
                  <InfoRow label="Updated" value={new Date(detail.updatedAt).toLocaleString()} />
                </div>
                <div className="space-y-2">
                  <div className="font-semibold">Canonical payload</div>
                  <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs leading-relaxed">
                    {JSON.stringify(detail.canonical, null, 2)}
                  </pre>
                </div>
                {detail.embedding && Array.isArray(detail.embedding) && detail.embedding.length > 0 && (
                  <div className="space-y-2">
                    <div className="font-semibold">Embedding preview</div>
                    <div className="text-xs text-muted-foreground">
                      Showing first 16 of {detail.embedding.length} dimensions
                    </div>
                    <div className="rounded-md bg-muted p-3 text-xs font-mono">
                      {detail.embedding.slice(0, 16).map((value, index) => (
                        <span key={index} className="mr-2">
                          {value.toFixed(4)}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </ScrollArea>
          ) : (
            <div className="text-sm text-muted-foreground">Select a document to view details.</div>
          )}
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function InfoRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="space-y-1">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className={mono ? "font-mono text-xs" : "text-sm"}>{value}</div>
    </div>
  );
}
