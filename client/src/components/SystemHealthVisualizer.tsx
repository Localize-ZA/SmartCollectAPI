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
      
      if (mainHealthTime > 2000) {
        newAlerts.push({
          id: Date.now().toString(),
          type: 'warning',
          message: `Main API response time is high: ${mainHealthTime}ms`,
          timestamp: new Date().toLocaleTimeString(),
          service: 'Main API'
        });
      }
      
      microservicesHealth.forEach(service => {
        if (service.status === 'unhealthy') {
          newAlerts.push({
            id: `${Date.now()}-${service.name}`,
            type: 'error',
            message: `${service.name} is unhealthy: ${service.error || 'Unknown error'}`,
            timestamp: new Date().toLocaleTimeString(),
            service: service.name
          });
        } else if (service.responseTime && service.responseTime > 5000) {
          newAlerts.push({
            id: `${Date.now()}-${service.name}-slow`,
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
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight">System Health Monitor</h2>
          <p className="text-muted-foreground">
            Real-time monitoring and analytics for system performance
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAutoRefresh(!autoRefresh)}
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
      </div>

      {/* Key Metrics */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">System Status</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
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

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Response Time</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{avgResponseTime}ms</div>
            <div className="flex items-center text-xs text-muted-foreground">
              {avgResponseTime < 1000 ? (
                <TrendingUp className="h-3 w-3 text-green-500 mr-1" />
              ) : (
                <TrendingDown className="h-3 w-3 text-red-500 mr-1" />
              )}
              Last {healthHistory.length} measurements
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Services</CardTitle>
            <Server className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{healthyServices}/{totalServices}</div>
            <p className="text-xs text-muted-foreground">
              {totalServices > 0 ? Math.round((healthyServices / totalServices) * 100) : 0}% healthy
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Alerts</CardTitle>
            <AlertTriangle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-500">{alerts.length}</div>
            <p className="text-xs text-muted-foreground">
              {alerts.filter(a => a.type === 'error').length} errors, {alerts.filter(a => a.type === 'warning').length} warnings
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Charts */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Response Time Chart */}
        <Card>
          <CardHeader>
            <CardTitle>Response Time Trend</CardTitle>
            <CardDescription>Last {MAX_DATA_POINTS} health checks</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={healthHistory}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="timestamp" />
                  <YAxis />
                  <Tooltip />
                  <Line 
                    type="monotone" 
                    dataKey="responseTime" 
                    stroke="#8884d8" 
                    strokeWidth={2}
                    dot={{ fill: '#8884d8' }}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Service Status Distribution */}
        <Card>
          <CardHeader>
            <CardTitle>Service Status Distribution</CardTitle>
            <CardDescription>Current status of all microservices</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={serviceStatusData}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
                    outerRadius={80}
                    fill="#8884d8"
                    dataKey="value"
                  >
                    {serviceStatusData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Microservices Detail */}
      <Card>
        <CardHeader>
          <CardTitle>Microservices Status</CardTitle>
          <CardDescription>Detailed status of all registered microservices</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {microservices.map((service) => (
              <div key={service.name} className="flex items-center justify-between p-4 border rounded-lg">
                <div className="flex items-center space-x-4">
                  {getStatusIcon(service.status)}
                  <div>
                    <h3 className="font-medium">{service.name}</h3>
                    <p className="text-sm text-muted-foreground">{service.url}</p>
                  </div>
                </div>
                <div className="flex items-center space-x-4">
                  <div className="text-right">
                    <p className="text-sm font-medium">{service.responseTime}ms</p>
                    <p className="text-xs text-muted-foreground">{service.lastChecked}</p>
                  </div>
                  <Badge variant={service.status === 'healthy' ? 'default' : 'destructive'}>
                    {service.status}
                  </Badge>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Recent Alerts */}
      {alerts.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Recent Alerts</CardTitle>
            <CardDescription>Latest system alerts and warnings</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 max-h-60 overflow-y-auto">
              {alerts.slice(0, 10).map((alert) => (
                <Alert key={alert.id} variant={alert.type === 'error' ? 'destructive' : 'default'}>
                  <AlertTriangle className="h-4 w-4" />
                  <AlertDescription>
                    <div className="flex justify-between items-start">
                      <div>
                        <span className="font-medium">{alert.service}: </span>
                        {alert.message}
                      </div>
                      <span className="text-xs text-muted-foreground ml-2">
                        {alert.timestamp}
                      </span>
                    </div>
                  </AlertDescription>
                </Alert>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}