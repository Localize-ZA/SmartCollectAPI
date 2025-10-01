"use client";
import { useState, useEffect } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  LineChart,
  Line,
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell
} from "recharts";
import {
  Activity,
  Server,
  Clock,
  TrendingUp,
  TrendingDown,
  AlertTriangle,
  CheckCircle,
  XCircle,
  RefreshCw
} from "lucide-react";
import { getHealth, getAllMicroservicesHealth, MicroserviceStatus } from "@/lib/api";

interface HealthMetric {
  timestamp: string;
  responseTime: number;
  status: 'healthy' | 'unhealthy' | 'unknown';
  cpu?: number;
  memory?: number;
}

interface SystemAlert {
  id: string;
  type: 'error' | 'warning' | 'info';
  message: string;
  timestamp: string;
  service: string;
}

const COLORS = ['#22c55e', '#ef4444', '#f59e0b', '#6b7280'];
const MAX_DATA_POINTS = 20;

export function SystemHealthVisualizer() {
  const [healthHistory, setHealthHistory] = useState<HealthMetric[]>([]);
  const [microservices, setMicroservices] = useState<MicroserviceStatus[]>([]);
  const [alerts, setAlerts] = useState<SystemAlert[]>([]);
  const [loading, setLoading] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);

  const collectHealthData = async () => {
    try {
      setLoading(true);
      
      // Get main API health
      const mainHealth = await getHealth();
      const mainHealthStart = Date.now();
      const mainHealthTime = Date.now() - mainHealthStart;
      
      // Get microservices health
      const microservicesHealth = await getAllMicroservicesHealth();
      setMicroservices(microservicesHealth);
      
      // Create new health metric
      const newMetric: HealthMetric = {
        timestamp: new Date().toLocaleTimeString(),
        responseTime: mainHealthTime,
        status: mainHealth.status === 'ok' ? 'healthy' : 'unhealthy',
        cpu: Math.random() * 100, // Simulated for now
        memory: Math.random() * 100, // Simulated for now
      };
      
      setHealthHistory(prev => {
        const updated = [...prev, newMetric];
        return updated.slice(-MAX_DATA_POINTS);
      });
      
      // Generate alerts based on metrics
      const newAlerts: SystemAlert[] = [];
      const baseTimestamp = Date.now();
      let alertCounter = 0;
      
      if (mainHealthTime > 2000) {
        newAlerts.push({
          id: `${baseTimestamp}-${alertCounter++}`,
          type: 'warning',
          message: `Main API response time is high: ${mainHealthTime}ms`,
          timestamp: new Date().toLocaleTimeString(),
          service: 'Main API'
        });
      }
      
      microservicesHealth.forEach(service => {
        if (service.status === 'unhealthy') {
          newAlerts.push({
            id: `${baseTimestamp}-${alertCounter++}`,
            type: 'error',
            message: `${service.name} is unhealthy: ${service.error || 'Unknown error'}`,
            timestamp: new Date().toLocaleTimeString(),
            service: service.name
          });
        } else if (service.responseTime && service.responseTime > 5000) {
          newAlerts.push({
            id: `${baseTimestamp}-${alertCounter++}`,
            type: 'warning',
            message: `${service.name} is responding slowly: ${service.responseTime}ms`,
            timestamp: new Date().toLocaleTimeString(),
            service: service.name
          });
        }
      });
      
      if (newAlerts.length > 0) {
        setAlerts(prev => [...newAlerts, ...prev].slice(0, 50)); // Keep last 50 alerts
      }
      
    } catch (error) {
      console.error('Failed to collect health data:', error);
      const errorMetric: HealthMetric = {
        timestamp: new Date().toLocaleTimeString(),
        responseTime: 0,
        status: 'unknown'
      };
      
      setHealthHistory(prev => {
        const updated = [...prev, errorMetric];
        return updated.slice(-MAX_DATA_POINTS);
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    collectHealthData();
    
    if (autoRefresh) {
      const interval = setInterval(collectHealthData, 10000); // Every 10 seconds
      return () => clearInterval(interval);
    }
  }, [autoRefresh]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy': return '#22c55e';
      case 'unhealthy': return '#ef4444';
      default: return '#6b7280';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy': return <CheckCircle className="h-4 w-4 text-green-500" />;
      case 'unhealthy': return <XCircle className="h-4 w-4 text-red-500" />;
      default: return <AlertTriangle className="h-4 w-4 text-gray-500" />;
    }
  };

  const avgResponseTime = healthHistory.length > 0 
    ? Math.round(healthHistory.reduce((sum, metric) => sum + metric.responseTime, 0) / healthHistory.length)
    : 0;

  const healthyServices = microservices.filter(s => s.status === 'healthy').length;
  const totalServices = microservices.length;

  const serviceStatusData = [
    { name: 'Healthy', value: microservices.filter(s => s.status === 'healthy').length, color: '#22c55e' },
    { name: 'Unhealthy', value: microservices.filter(s => s.status === 'unhealthy').length, color: '#ef4444' },
    { name: 'Unknown', value: microservices.filter(s => s.status === 'unknown').length, color: '#6b7280' },
  ].filter(item => item.value > 0);

  return (
    <div className="space-y-6">
      {/* Controls */}
      <div className="flex items-center justify-end gap-2">
        <Button
          variant={autoRefresh ? 'default' : 'outline'}
          size="sm"
          onClick={() => setAutoRefresh(!autoRefresh)}
          className={autoRefresh ? 'shadow-lg ring-2 ring-primary/20' : ''}
        >
          <Activity className={`h-4 w-4 mr-2 ${autoRefresh ? 'animate-pulse' : ''}`} />
          Auto Refresh {autoRefresh ? 'On' : 'Off'}
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={collectHealthData}
          disabled={loading}
        >
          <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {/* Key Metrics */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">System Status</CardTitle>
            <Activity className="h-4 w-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="flex items-center space-x-2">
              {getStatusIcon(healthHistory[healthHistory.length - 1]?.status || 'unknown')}
              <div className="text-2xl font-bold">
                {healthHistory[healthHistory.length - 1]?.status?.toUpperCase() || 'UNKNOWN'}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Response Time</CardTitle>
            <Clock className="h-4 w-4 text-warning" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-primary">{avgResponseTime}ms</div>
            <div className="flex items-center text-xs text-muted-foreground mt-1">
              {avgResponseTime < 1000 ? (
                <>
                  <TrendingUp className="h-3 w-3 text-success mr-1" />
                  <span className="text-success">Excellent</span>
                </>
              ) : (
                <>
                  <TrendingDown className="h-3 w-3 text-destructive mr-1" />
                  <span className="text-destructive">Slow</span>
                </>
              )}
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Services</CardTitle>
            <Server className="h-4 w-4 text-success" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-success">{healthyServices}/{totalServices}</div>
            <p className="text-xs text-muted-foreground mt-1">
              {totalServices > 0 ? Math.round((healthyServices / totalServices) * 100) : 0}% healthy
            </p>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Alerts</CardTitle>
            <AlertTriangle className="h-4 w-4 text-destructive" />
          </CardHeader>
          <CardContent>
            <div className={`text-2xl font-bold ${alerts.length > 0 ? 'text-destructive' : 'text-success'}`}>
              {alerts.length}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {alerts.filter(a => a.type === 'error').length} errors, {alerts.filter(a => a.type === 'warning').length} warnings
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Charts */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Response Time Chart */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
            <div className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-primary" />
              <div>
                <CardTitle className="text-lg">Response Time Trend</CardTitle>
                <CardDescription>Last {MAX_DATA_POINTS} health checks</CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="pt-6">
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={healthHistory}>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis dataKey="timestamp" fontSize={12} />
                  <YAxis fontSize={12} />
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: 'oklch(1 0.01 264)', 
                      border: '1px solid oklch(0.90 0.01 264)',
                      borderRadius: '8px'
                    }}
                  />
                  <Line 
                    type="monotone" 
                    dataKey="responseTime" 
                    stroke="oklch(0.55 0.22 264)" 
                    strokeWidth={3}
                    dot={{ fill: 'oklch(0.55 0.22 264)', r: 4 }}
                    activeDot={{ r: 6 }}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Service Status Distribution */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
            <div className="flex items-center gap-2">
              <Server className="h-5 w-5 text-success" />
              <div>
                <CardTitle className="text-lg">Service Status Distribution</CardTitle>
                <CardDescription>Current status of all microservices</CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="pt-6">
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={serviceStatusData}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
                    outerRadius={90}
                    fill="#8884d8"
                    dataKey="value"
                  >
                    {serviceStatusData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: 'oklch(1 0.01 264)', 
                      border: '1px solid oklch(0.90 0.01 264)',
                      borderRadius: '8px'
                    }}
                  />
                </PieChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Microservices Detail */}
      <Card className="hover-lift glass-effect ring-1 ring-border/50">
        <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
          <div className="flex items-center gap-2">
            <Server className="h-5 w-5 text-primary" />
            <div>
              <CardTitle className="text-lg">Microservices Status</CardTitle>
              <CardDescription>Detailed status of all registered microservices</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="pt-6">
          <div className="space-y-3">
            {microservices.map((service) => {
              const statusColor = service.status === 'healthy' ? 'text-success bg-success/10 ring-success/20' :
                                service.status === 'unhealthy' ? 'text-destructive bg-destructive/10 ring-destructive/20' :
                                'text-warning bg-warning/10 ring-warning/20';
              
              return (
                <div 
                  key={service.name} 
                  className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4">
                      <div className={`flex h-10 w-10 items-center justify-center rounded-lg ring-1 ${statusColor}`}>
                        {getStatusIcon(service.status)}
                      </div>
                      <div>
                        <h3 className="font-semibold text-sm">{service.name}</h3>
                        <p className="text-xs text-muted-foreground font-mono">{service.url}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-4">
                      <div className="text-right">
                        <p className="text-sm font-semibold text-primary">{service.responseTime}ms</p>
                        <p className="text-xs text-muted-foreground">{new Date(service.lastChecked).toLocaleTimeString()}</p>
                      </div>
                      <Badge 
                        variant="outline"
                        className={`${statusColor} border-0`}
                      >
                        {service.status}
                      </Badge>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Recent Alerts */}
      {alerts.length > 0 && (
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive" />
              <div>
                <CardTitle className="text-lg">Recent Alerts</CardTitle>
                <CardDescription>Latest system alerts and warnings</CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="pt-6">
            <div className="space-y-2 max-h-80 overflow-y-auto scrollbar-custom pr-2">
              {alerts.slice(0, 10).map((alert) => {
                const alertConfig = {
                  error: { color: 'text-destructive', bgColor: 'bg-destructive/10', ringColor: 'ring-destructive/20' },
                  warning: { color: 'text-warning', bgColor: 'bg-warning/10', ringColor: 'ring-warning/20' },
                  info: { color: 'text-primary', bgColor: 'bg-primary/10', ringColor: 'ring-primary/20' }
                };
                
                const config = alertConfig[alert.type];
                
                return (
                  <div 
                    key={alert.id}
                    className={`rounded-xl p-3 ring-1 ${config.bgColor} ${config.ringColor}`}
                  >
                    <div className="flex items-start gap-3">
                      <AlertTriangle className={`h-4 w-4 flex-shrink-0 mt-0.5 ${config.color}`} />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between gap-2">
                          <div className="flex-1">
                            <span className={`font-semibold text-sm ${config.color}`}>{alert.service}: </span>
                            <span className="text-sm">{alert.message}</span>
                          </div>
                          <span className="text-xs text-muted-foreground flex-shrink-0">
                            {alert.timestamp}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}