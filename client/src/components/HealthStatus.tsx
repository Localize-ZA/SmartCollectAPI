"use client";
import { useEffect, useState } from "react";
import { getHealth, API_BASE } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export function HealthStatus() {
  const [status, setStatus] = useState<string>("unknown");
  const [ts, setTs] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setLoading(true);
    setError(null);
    try {
      const h = await getHealth();
      setStatus(h.status);
      setTs(new Date(h.ts).toLocaleString());
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
      setStatus("unreachable");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  const badgeVariant = status === "ok" ? "default" : status === "unreachable" ? "destructive" : "secondary";

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Server Health</CardTitle>
        <CardDescription>Base URL: {API_BASE}</CardDescription>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Badge variant={badgeVariant}>{status}</Badge>
          {ts && <span className="text-sm text-muted-foreground">{ts}</span>}
          {error && <span className="text-sm text-destructive">{error}</span>}
        </div>
        <Button onClick={refresh} disabled={loading} variant="outline" size="sm">
          {loading ? "Checking…" : "Refresh"}
        </Button>
      </CardContent>
    </Card>
  );
}
