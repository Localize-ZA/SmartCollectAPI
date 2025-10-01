"use client";
import { useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  AlertTriangle,
  Bell,
  CheckCircle2,
  XCircle,
  Clock,
  Mail,
  Trash2,
  Plus,
  Settings,
  Filter,
  Search,
  TrendingUp
} from "lucide-react";

interface Alert {
  id: string;
  type: 'error' | 'warning' | 'info' | 'success';
  severity: 'critical' | 'high' | 'medium' | 'low';
  message: string;
  source: string;
  timestamp: Date;
  acknowledged: boolean;
  resolved: boolean;
}

interface AlertRule {
  id: string;
  name: string;
  enabled: boolean;
  condition: string;
  severity: 'critical' | 'high' | 'medium' | 'low';
  notifyEmail: boolean;
  notifySlack: boolean;
}

export function AlertsManager() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [alertRules, setAlertRules] = useState<AlertRule[]>([]);
  const [filter, setFilter] = useState<'all' | 'error' | 'warning' | 'info' | 'success'>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [showResolved, setShowResolved] = useState(false);

  // Mock data - replace with actual API calls
  useEffect(() => {
    const mockAlerts: Alert[] = [
      {
        id: '1',
        type: 'error',
        severity: 'critical',
        message: 'Sentence-Transformers service is down',
        source: 'Microservices Monitor',
        timestamp: new Date(Date.now() - 5 * 60000),
        acknowledged: false,
        resolved: false
      },
      {
        id: '2',
        type: 'warning',
        severity: 'high',
        message: 'Response time exceeds 3000ms threshold',
        source: 'Performance Monitor',
        timestamp: new Date(Date.now() - 15 * 60000),
        acknowledged: true,
        resolved: false
      },
      {
        id: '3',
        type: 'warning',
        severity: 'medium',
        message: 'Storage usage at 75%',
        source: 'System Monitor',
        timestamp: new Date(Date.now() - 30 * 60000),
        acknowledged: true,
        resolved: false
      },
      {
        id: '4',
        type: 'info',
        severity: 'low',
        message: 'Database backup completed successfully',
        source: 'Backup Service',
        timestamp: new Date(Date.now() - 2 * 3600000),
        acknowledged: true,
        resolved: true
      },
      {
        id: '5',
        type: 'success',
        severity: 'low',
        message: 'All microservices health check passed',
        source: 'Health Monitor',
        timestamp: new Date(Date.now() - 10 * 60000),
        acknowledged: true,
        resolved: true
      }
    ];

    const mockRules: AlertRule[] = [
      {
        id: '1',
        name: 'Service Down Alert',
        enabled: true,
        condition: 'service.status === "unhealthy"',
        severity: 'critical',
        notifyEmail: true,
        notifySlack: true
      },
      {
        id: '2',
        name: 'High Response Time',
        enabled: true,
        condition: 'responseTime > 3000',
        severity: 'high',
        notifyEmail: true,
        notifySlack: false
      },
      {
        id: '3',
        name: 'Storage Threshold',
        enabled: true,
        condition: 'storageUsage > 80',
        severity: 'medium',
        notifyEmail: false,
        notifySlack: true
      },
      {
        id: '4',
        name: 'Failed Uploads',
        enabled: false,
        condition: 'failedUploads > 5',
        severity: 'high',
        notifyEmail: true,
        notifySlack: true
      }
    ];

    setAlerts(mockAlerts);
    setAlertRules(mockRules);
  }, []);

  const filteredAlerts = alerts.filter(alert => {
    const matchesFilter = filter === 'all' || alert.type === filter;
    const matchesSearch = alert.message.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         alert.source.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesResolved = showResolved || !alert.resolved;
    return matchesFilter && matchesSearch && matchesResolved;
  });

  const alertStats = {
    total: alerts.length,
    critical: alerts.filter(a => a.severity === 'critical' && !a.resolved).length,
    unacknowledged: alerts.filter(a => !a.acknowledged && !a.resolved).length,
    resolved: alerts.filter(a => a.resolved).length
  };

  const getAlertConfig = (type: string) => {
    switch (type) {
      case 'error':
        return { icon: XCircle, color: 'text-destructive', bgColor: 'bg-destructive/10', ringColor: 'ring-destructive/20' };
      case 'warning':
        return { icon: AlertTriangle, color: 'text-warning', bgColor: 'bg-warning/10', ringColor: 'ring-warning/20' };
      case 'success':
        return { icon: CheckCircle2, color: 'text-success', bgColor: 'bg-success/10', ringColor: 'ring-success/20' };
      default:
        return { icon: Bell, color: 'text-primary', bgColor: 'bg-primary/10', ringColor: 'ring-primary/20' };
    }
  };

  const getSeverityBadge = (severity: string) => {
    const config = {
      critical: 'bg-destructive/10 text-destructive ring-destructive/20',
      high: 'bg-warning/10 text-warning ring-warning/20',
      medium: 'bg-primary/10 text-primary ring-primary/20',
      low: 'bg-muted text-muted-foreground ring-muted/20'
    };
    return config[severity as keyof typeof config] || config.low;
  };

  const acknowledgeAlert = (id: string) => {
    setAlerts(prev => prev.map(alert => 
      alert.id === id ? { ...alert, acknowledged: true } : alert
    ));
  };

  const resolveAlert = (id: string) => {
    setAlerts(prev => prev.map(alert => 
      alert.id === id ? { ...alert, resolved: true } : alert
    ));
  };

  const deleteAlert = (id: string) => {
    setAlerts(prev => prev.filter(alert => alert.id !== id));
  };

  const toggleRule = (id: string) => {
    setAlertRules(prev => prev.map(rule => 
      rule.id === id ? { ...rule, enabled: !rule.enabled } : rule
    ));
  };

  return (
    <div className="space-y-6">
      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Total Alerts</p>
                <p className="text-3xl font-bold">{alertStats.total}</p>
              </div>
              <Bell className="h-8 w-8 text-primary opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Critical</p>
                <p className="text-3xl font-bold text-destructive">{alertStats.critical}</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-destructive opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Unacknowledged</p>
                <p className="text-3xl font-bold text-warning">{alertStats.unacknowledged}</p>
              </div>
              <Clock className="h-8 w-8 text-warning opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Resolved</p>
                <p className="text-3xl font-bold text-success">{alertStats.resolved}</p>
              </div>
              <CheckCircle2 className="h-8 w-8 text-success opacity-50" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Main Content */}
      <Tabs defaultValue="alerts" className="space-y-6">
        <TabsList className="grid w-full grid-cols-2 max-w-md">
          <TabsTrigger value="alerts">Active Alerts</TabsTrigger>
          <TabsTrigger value="rules">Alert Rules</TabsTrigger>
        </TabsList>

        {/* Active Alerts Tab */}
        <TabsContent value="alerts" className="space-y-4">
          {/* Filters */}
          <Card className="hover-lift glass-effect ring-1 ring-border/50">
            <CardHeader>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Filter className="h-5 w-5 text-primary" />
                  <CardTitle className="text-lg">Filters</CardTitle>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-2">
                {(['all', 'error', 'warning', 'info', 'success'] as const).map((type) => (
                  <Button
                    key={type}
                    variant={filter === type ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setFilter(type)}
                    className="capitalize"
                  >
                    {type}
                  </Button>
                ))}
              </div>
              
              <div className="flex items-center gap-4">
                <div className="flex-1 relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    placeholder="Search alerts..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
                <div className="flex items-center gap-2">
                  <Switch
                    checked={showResolved}
                    onCheckedChange={setShowResolved}
                    id="show-resolved"
                  />
                  <Label htmlFor="show-resolved" className="text-sm">Show Resolved</Label>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Alerts List */}
          <Card className="hover-lift glass-effect ring-1 ring-border/50">
            <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
              <div className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-primary" />
                <div>
                  <CardTitle className="text-lg">Alerts ({filteredAlerts.length})</CardTitle>
                  <CardDescription>System-wide alerts and notifications</CardDescription>
                </div>
              </div>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="space-y-3 max-h-[600px] overflow-y-auto scrollbar-custom pr-2">
                {filteredAlerts.length === 0 ? (
                  <div className="rounded-xl bg-muted/30 p-8 text-center ring-1 ring-border/50">
                    <CheckCircle2 className="h-12 w-12 mx-auto text-success/50 mb-3" />
                    <p className="text-sm font-medium text-muted-foreground">No alerts found</p>
                    <p className="text-xs text-muted-foreground/70 mt-1">System is running smoothly</p>
                  </div>
                ) : (
                  filteredAlerts.map((alert) => {
                    const config = getAlertConfig(alert.type);
                    const Icon = config.icon;
                    
                    return (
                      <div
                        key={alert.id}
                        className={`group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all ${
                          alert.resolved ? 'opacity-60' : ''
                        }`}
                      >
                        <div className="flex items-start gap-3">
                          <div className={`flex h-10 w-10 items-center justify-center rounded-lg flex-shrink-0 ring-1 ${config.bgColor} ${config.color} ${config.ringColor}`}>
                            <Icon className="h-5 w-5" />
                          </div>
                          
                          <div className="flex-1 min-w-0">
                            <div className="flex items-start justify-between gap-2 mb-2">
                              <div className="flex-1">
                                <p className="text-sm font-semibold">{alert.message}</p>
                                <div className="flex items-center gap-2 mt-1">
                                  <Badge variant="outline" className={`text-[10px] px-2 py-0 ${getSeverityBadge(alert.severity)} border-0`}>
                                    {alert.severity}
                                  </Badge>
                                  <span className="text-xs text-muted-foreground">{alert.source}</span>
                                </div>
                              </div>
                              <span className="text-xs text-muted-foreground flex-shrink-0">
                                {alert.timestamp.toLocaleTimeString()}
                              </span>
                            </div>
                            
                            <div className="flex items-center gap-2">
                              {!alert.acknowledged && !alert.resolved && (
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => acknowledgeAlert(alert.id)}
                                  className="h-7 text-xs"
                                >
                                  <CheckCircle2 className="h-3 w-3 mr-1" />
                                  Acknowledge
                                </Button>
                              )}
                              {!alert.resolved && (
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => resolveAlert(alert.id)}
                                  className="h-7 text-xs"
                                >
                                  <CheckCircle2 className="h-3 w-3 mr-1" />
                                  Resolve
                                </Button>
                              )}
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => deleteAlert(alert.id)}
                                className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 transition-opacity"
                              >
                                <Trash2 className="h-3.5 w-3.5 text-destructive" />
                              </Button>
                            </div>
                          </div>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Alert Rules Tab */}
        <TabsContent value="rules" className="space-y-4">
          <Card className="hover-lift glass-effect ring-1 ring-border/50">
            <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Settings className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">Alert Rules</CardTitle>
                    <CardDescription>Configure automated alert triggers</CardDescription>
                  </div>
                </div>
                <Button size="sm" className="gap-2">
                  <Plus className="h-4 w-4" />
                  Add Rule
                </Button>
              </div>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="space-y-3">
                {alertRules.map((rule) => (
                  <div
                    key={rule.id}
                    className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-2">
                          <h4 className="text-sm font-semibold">{rule.name}</h4>
                          <Badge variant="outline" className={`text-[10px] px-2 py-0 ${getSeverityBadge(rule.severity)} border-0`}>
                            {rule.severity}
                          </Badge>
                          <Switch
                            checked={rule.enabled}
                            onCheckedChange={() => toggleRule(rule.id)}
                          />
                        </div>
                        
                        <code className="text-xs bg-muted/50 px-2 py-1 rounded font-mono block mb-2">
                          {rule.condition}
                        </code>
                        
                        <div className="flex items-center gap-3 text-xs text-muted-foreground">
                          {rule.notifyEmail && (
                            <div className="flex items-center gap-1">
                              <Mail className="h-3 w-3" />
                              <span>Email</span>
                            </div>
                          )}
                          {rule.notifySlack && (
                            <div className="flex items-center gap-1">
                              <Bell className="h-3 w-3" />
                              <span>Slack</span>
                            </div>
                          )}
                        </div>
                      </div>
                      
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-8 w-8 p-0"
                      >
                        <Settings className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
