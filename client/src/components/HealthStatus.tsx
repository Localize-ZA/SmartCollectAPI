"use client";
import { useGlobalHealthStatus } from "@/components/HealthProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Activity, CheckCircle2, XCircle, AlertCircle, RefreshCw } from "lucide-react";

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

  // Get status icon and color
  const getStatusDisplay = () => {
    if (isHealthy) {
      return {
        icon: CheckCircle2,
        color: "text-success",
        bgColor: "bg-success/10",
        label: "Healthy"
      };
    } else if (isUnreachable) {
      return {
        icon: XCircle,
        color: "text-destructive",
        bgColor: "bg-destructive/10",
        label: "Unreachable"
      };
    } else {
      return {
        icon: AlertCircle,
        color: "text-warning",
        bgColor: "bg-warning/10",
        label: "Warning"
      };
    }
  };

  const statusDisplay = getStatusDisplay();
  const StatusIcon = statusDisplay.icon;

  return (
    <Card className="w-full hover-lift glass-effect ring-1 ring-border/50">
      <CardHeader className="pb-3">
        <div className="flex items-center gap-3">
          <div className={`flex h-10 w-10 items-center justify-center rounded-xl ${statusDisplay.bgColor}`}>
            <Activity className={`h-5 w-5 ${statusDisplay.color}`} />
          </div>
          <div>
            <CardTitle className="text-base">Server Health</CardTitle>
            <CardDescription className="text-xs">Main API Endpoint</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Status Display */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={`flex h-12 w-12 items-center justify-center rounded-xl ${statusDisplay.bgColor} ring-2 ring-offset-2 ring-offset-card ${isHealthy ? 'ring-success/20' : isUnreachable ? 'ring-destructive/20' : 'ring-warning/20'}`}>
              <StatusIcon className={`h-6 w-6 ${statusDisplay.color}`} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <p className="text-lg font-bold">{statusDisplay.label}</p>
                <Badge variant={badgeVariant} className="relative">
                  {isRefreshing && (
                    <div className="absolute -top-0.5 -right-0.5 w-2 h-2 bg-primary rounded-full animate-pulse" />
                  )}
                  {healthStatus.status}
                </Badge>
              </div>
              <p className="text-xs text-muted-foreground">{API_BASE}</p>
            </div>
          </div>
          
          <Button 
            onClick={refresh} 
            disabled={isRefreshing} 
            variant="outline" 
            size="sm"
            className="hover:bg-primary hover:text-primary-foreground transition-colors"
          >
            <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${isRefreshing ? 'animate-spin' : ''}`} />
            {isRefreshing ? "Checking" : "Refresh"}
          </Button>
        </div>

        {/* Timestamp Info */}
        <div className="flex flex-col gap-1 pt-2 border-t border-border/50">
          {timestamp && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span className="font-medium">Server Time:</span>
              <span className="font-mono">{timestamp}</span>
            </div>
          )}
          {lastCheckedText && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span className="font-medium">{lastCheckedText}</span>
            </div>
          )}
        </div>

        {/* Error Display */}
        {error && (
          <div className="rounded-lg bg-destructive/10 p-3 ring-1 ring-destructive/20">
            <p className="text-xs text-destructive font-medium">{error}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
