"use client";
import { useEffect, useState } from "react";
import { getAllMicroservicesHealth, MicroserviceStatus } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { RefreshCw, ExternalLink, Clock, Zap, Server, CheckCircle2, XCircle, AlertCircle, Activity } from "lucide-react";

export function MicroservicesStatus() {
  const [services, setServices] = useState<MicroserviceStatus[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function checkServices() {
    setLoading(true);
    setError(null);
    try {
      const statuses = await getAllMicroservicesHealth();
      setServices(statuses);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setError(msg);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    checkServices();
    // Auto-refresh every 30 seconds
    const interval = setInterval(checkServices, 30000);
    return () => clearInterval(interval);
  }, []);

  const getStatusVariant = (status: MicroserviceStatus['status']) => {
    switch (status) {
      case 'healthy': return 'default' as const;
      case 'unhealthy': return 'destructive' as const;
      case 'unknown': return 'secondary' as const;
    }
  };

  const getStatusColor = (status: MicroserviceStatus['status']) => {
    switch (status) {
      case 'healthy': return 'text-green-600';
      case 'unhealthy': return 'text-red-600';
      case 'unknown': return 'text-gray-600';
    }
  };

  const totalServices = services.length;
  const healthyServices = services.filter(s => s.status === 'healthy').length;
  const unhealthyServices = services.filter(s => s.status === 'unhealthy').length;

  return (
    <Card className="w-full hover-lift glass-effect ring-1 ring-border/50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-chart-4/20 to-chart-4/5 ring-1 ring-chart-4/10">
              <Server className="h-5 w-5 text-chart-4" />
            </div>
            <div>
              <CardTitle className="text-base">Microservices Status</CardTitle>
              <CardDescription className="text-xs">
                Python ML services health monitoring
              </CardDescription>
            </div>
          </div>
          <Button 
            onClick={checkServices} 
            disabled={loading} 
            variant="outline" 
            size="sm"
            className="hover:bg-primary hover:text-primary-foreground transition-colors"
          >
            <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Summary Cards */}
        <div className="grid grid-cols-3 gap-3">
          <div className="rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 text-center hover-lift">
            <div className="flex justify-center mb-2">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <Server className="h-5 w-5 text-primary" />
              </div>
            </div>
            <div className="text-2xl font-bold">{totalServices}</div>
            <div className="text-xs text-muted-foreground mt-1">Total Services</div>
          </div>
          <div className="rounded-xl border border-border/50 bg-gradient-to-br from-card to-success/5 p-4 text-center hover-lift">
            <div className="flex justify-center mb-2">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-success/10">
                <CheckCircle2 className="h-5 w-5 text-success" />
              </div>
            </div>
            <div className="text-2xl font-bold text-success">{healthyServices}</div>
            <div className="text-xs text-muted-foreground mt-1">Healthy</div>
          </div>
          <div className="rounded-xl border border-border/50 bg-gradient-to-br from-card to-destructive/5 p-4 text-center hover-lift">
            <div className="flex justify-center mb-2">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-destructive/10">
                <XCircle className="h-5 w-5 text-destructive" />
              </div>
            </div>
            <div className="text-2xl font-bold text-destructive">{unhealthyServices}</div>
            <div className="text-xs text-muted-foreground mt-1">Unhealthy</div>
          </div>
        </div>

        {error && (
          <div className="rounded-lg bg-destructive/10 p-4 ring-1 ring-destructive/20">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-destructive flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-destructive">Connection Error</p>
                <p className="text-xs text-destructive/80 mt-1">{error}</p>
              </div>
            </div>
          </div>
        )}

        {/* Services List */}
        <div className="space-y-3">
          {services.map((service, index) => {
            const StatusIcon = service.status === 'healthy' ? CheckCircle2 : 
                             service.status === 'unhealthy' ? XCircle : AlertCircle;
            const statusColor = service.status === 'healthy' ? 'text-success bg-success/10 ring-success/20' :
                              service.status === 'unhealthy' ? 'text-destructive bg-destructive/10 ring-destructive/20' :
                              'text-warning bg-warning/10 ring-warning/20';
            
            return (
              <div 
                key={index} 
                className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
              >
                <div className="flex items-start justify-between gap-4">
                  {/* Service Info */}
                  <div className="flex items-start gap-3 flex-1 min-w-0">
                    <div className={`flex h-10 w-10 items-center justify-center rounded-lg ring-1 flex-shrink-0 ${statusColor}`}>
                      <StatusIcon className="h-5 w-5" />
                    </div>
                    
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-2">
                        <h4 className="font-semibold text-sm">{service.name}</h4>
                        <Badge 
                          variant={getStatusVariant(service.status)}
                          className="text-[10px] px-2 py-0.5"
                        >
                          {service.status}
                        </Badge>
                      </div>
                      
                      <div className="space-y-1.5">
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          <Server className="h-3 w-3 flex-shrink-0" />
                          <span className="truncate font-mono text-[11px]">{service.url}</span>
                        </div>
                        
                        <div className="flex items-center gap-4">
                          {service.responseTime && (
                            <div className="flex items-center gap-1.5 text-xs">
                              <Zap className={`h-3 w-3 ${service.responseTime < 100 ? 'text-success' : service.responseTime < 500 ? 'text-warning' : 'text-destructive'}`} />
                              <span className="font-mono font-medium">{service.responseTime}ms</span>
                            </div>
                          )}
                          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                            <Clock className="h-3 w-3" />
                            <span>{new Date(service.lastChecked).toLocaleTimeString()}</span>
                          </div>
                          {service.version && (
                            <div className="text-xs text-muted-foreground font-mono">
                              v{service.version}
                            </div>
                          )}
                        </div>
                      </div>
                      
                      {service.error && (
                        <div className="mt-2 rounded-lg bg-destructive/10 p-2 ring-1 ring-destructive/20">
                          <p className="text-xs text-destructive font-medium">{service.error}</p>
                        </div>
                      )}
                    </div>
                  </div>
                  
                  {/* Status Indicator & Action */}
                  <div className="flex flex-col items-end gap-2">
                    <div className={`w-2 h-2 rounded-full status-indicator ${
                      service.status === 'healthy' ? 'bg-success' :
                      service.status === 'unhealthy' ? 'bg-destructive' : 'bg-warning'
                    }`} />
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => window.open(service.url, '_blank')}
                      className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity"
                    >
                      <ExternalLink className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {services.length === 0 && !loading && (
          <div className="rounded-xl bg-muted/30 p-8 text-center ring-1 ring-border/50">
            <Server className="h-12 w-12 mx-auto text-muted-foreground/50 mb-3" />
            <p className="text-sm font-medium text-muted-foreground">No microservices configured</p>
            <p className="text-xs text-muted-foreground/70 mt-1">Services will appear here once they're running</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}