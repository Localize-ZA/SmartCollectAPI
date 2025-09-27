"use client";
import { useEffect, useState } from "react";
import { getAllMicroservicesHealth, MicroserviceStatus } from "@/lib/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { RefreshCw, ExternalLink, Clock, Zap } from "lucide-react";

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
    <Card className="w-full">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle>Microservices Status</CardTitle>
            <CardDescription>
              Monitor health and performance of all microservices
            </CardDescription>
          </div>
          <Button onClick={checkServices} disabled={loading} variant="outline" size="sm">
            <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {/* Summary */}
        <div className="grid grid-cols-3 gap-4 mb-6">
          <div className="text-center">
            <div className="text-2xl font-bold text-blue-600">{totalServices}</div>
            <div className="text-sm text-muted-foreground">Total Services</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-green-600">{healthyServices}</div>
            <div className="text-sm text-muted-foreground">Healthy</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-red-600">{unhealthyServices}</div>
            <div className="text-sm text-muted-foreground">Unhealthy</div>
          </div>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4">
            <p className="text-red-800 text-sm">{error}</p>
          </div>
        )}

        {/* Services List */}
        <div className="space-y-3">
          {services.map((service, index) => (
            <div key={index} className="flex items-center justify-between p-4 border rounded-lg">
              <div className="flex items-center gap-4 flex-1 min-w-0">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h4 className="font-medium truncate">{service.name}</h4>
                    <Badge variant={getStatusVariant(service.status)}>
                      {service.status}
                    </Badge>
                  </div>
                  <div className="flex items-center gap-4 text-sm text-muted-foreground">
                    <span className="truncate">{service.url}</span>
                    {service.responseTime && (
                      <div className="flex items-center gap-1">
                        <Zap className="h-3 w-3" />
                        <span>{service.responseTime}ms</span>
                      </div>
                    )}
                    <div className="flex items-center gap-1">
                      <Clock className="h-3 w-3" />
                      <span>{new Date(service.lastChecked).toLocaleTimeString()}</span>
                    </div>
                  </div>
                  {service.error && (
                    <p className="text-xs text-red-600 mt-1">{service.error}</p>
                  )}
                  {service.version && (
                    <p className="text-xs text-muted-foreground mt-1">v{service.version}</p>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-2">
                <div className={`w-3 h-3 rounded-full ${
                  service.status === 'healthy' ? 'bg-green-500' :
                  service.status === 'unhealthy' ? 'bg-red-500' : 'bg-gray-400'
                }`} />
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => window.open(service.url, '_blank')}
                >
                  <ExternalLink className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        {services.length === 0 && !loading && (
          <div className="text-center text-muted-foreground py-8">
            No microservices configured
          </div>
        )}
      </CardContent>
    </Card>
  );
}