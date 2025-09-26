"use client";
import { useGlobalHealthStatus } from "@/components/HealthProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5082";

export function HealthStatus() {
  const { healthStatus, isRefreshing, error, refresh } = useGlobalHealthStatus();
  
  const isHealthy = healthStatus.status === 'ok';
  const isUnreachable = healthStatus.status === 'unreachable';
  const badgeVariant = isHealthy ? "default" : isUnreachable ? "destructive" : "secondary";
  
  // Format timestamps
  const timestamp = healthStatus.ts ? new Date(healthStatus.ts).toLocaleString() : '';
  const lastCheckedText = healthStatus.lastChecked 
    ? `Last checked: ${new Date(healthStatus.lastChecked).toLocaleTimeString()}`
    : '';

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Server Health</CardTitle>
        <CardDescription>Base URL: {API_BASE}</CardDescription>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-4">
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-3">
            <Badge variant={badgeVariant} className="relative">
              {isRefreshing && (
                <div className="absolute -top-1 -right-1 w-2 h-2 bg-blue-500 rounded-full animate-pulse" />
              )}
              {healthStatus.status}
            </Badge>
            {timestamp && <span className="text-sm text-muted-foreground">{timestamp}</span>}
          </div>
          {lastCheckedText && (
            <span className="text-xs text-muted-foreground/70">{lastCheckedText}</span>
          )}
          {error && <span className="text-sm text-destructive">{error}</span>}
        </div>
        <Button onClick={refresh} disabled={isRefreshing} variant="outline" size="sm">
          {isRefreshing ? "Checking…" : "Refresh"}
        </Button>
      </CardContent>
    </Card>
  );
}
