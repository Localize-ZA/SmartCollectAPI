"use client";
import { useGlobalHealthStatus } from "@/components/HealthProvider";

export function ServerStatusIndicator() {
  const { healthStatus, isRefreshing } = useGlobalHealthStatus();
  
  const isHealthy = healthStatus.status === 'ok';
  const isUnreachable = healthStatus.status === 'unreachable';
  
  return (
    <div className="fixed top-4 right-4 z-50">
      <div className="flex items-center gap-2 bg-background/80 backdrop-blur-sm border rounded-lg px-3 py-1.5 shadow-sm">
        <div className={`w-2 h-2 rounded-full ${
          isHealthy ? 'bg-green-500' : 
          isUnreachable ? 'bg-red-500' : 
          'bg-yellow-500'
        } ${isRefreshing ? 'animate-pulse' : ''}`} />
        <span className="text-xs text-muted-foreground">
          {isRefreshing ? 'Updating...' : 'Server status'}
        </span>
      </div>
    </div>
  );
}