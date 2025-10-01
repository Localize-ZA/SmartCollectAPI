"use client";
import { useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Mail,
  Send,
  Settings,
  CheckCircle2,
  XCircle,
  Clock,
  Server,
  Key,
  User,
  Eye,
  EyeOff,
  TestTube,
  Activity,
  FileText,
  TrendingUp
} from "lucide-react";

interface EmailConfig {
  smtpHost: string;
  smtpPort: number;
  smtpUser: string;
  smtpPassword: string;
  fromEmail: string;
  fromName: string;
  useTls: boolean;
  enabled: boolean;
}

interface EmailTemplate {
  id: string;
  name: string;
  subject: string;
  body: string;
  type: 'notification' | 'alert' | 'report';
}

interface EmailLog {
  id: string;
  to: string;
  subject: string;
  status: 'sent' | 'failed' | 'pending';
  timestamp: Date;
  error?: string;
}

export function EmailServiceManager() {
  const [config, setConfig] = useState<EmailConfig>({
    smtpHost: 'smtp.gmail.com',
    smtpPort: 587,
    smtpUser: '',
    smtpPassword: '',
    fromEmail: 'noreply@smartcollect.io',
    fromName: 'SmartCollect',
    useTls: true,
    enabled: true
  });

  const [templates, setTemplates] = useState<EmailTemplate[]>([]);
  const [emailLogs, setEmailLogs] = useState<EmailLog[]>([]);
  const [showPassword, setShowPassword] = useState(false);
  const [testEmail, setTestEmail] = useState('');
  const [isTesting, setIsTesting] = useState(false);

  // Mock data
  useEffect(() => {
    const mockTemplates: EmailTemplate[] = [
      {
        id: '1',
        name: 'Processing Complete',
        subject: 'Document Processing Complete',
        body: 'Your document has been successfully processed.',
        type: 'notification'
      },
      {
        id: '2',
        name: 'Service Alert',
        subject: 'Service Health Alert',
        body: 'A service health issue has been detected.',
        type: 'alert'
      },
      {
        id: '3',
        name: 'Daily Report',
        subject: 'Daily Processing Report',
        body: 'Here is your daily processing summary.',
        type: 'report'
      }
    ];

    const mockLogs: EmailLog[] = [
      {
        id: '1',
        to: 'user@example.com',
        subject: 'Document Processing Complete',
        status: 'sent',
        timestamp: new Date(Date.now() - 5 * 60000)
      },
      {
        id: '2',
        to: 'admin@example.com',
        subject: 'Service Health Alert',
        status: 'sent',
        timestamp: new Date(Date.now() - 15 * 60000)
      },
      {
        id: '3',
        to: 'user@example.com',
        subject: 'Daily Report',
        status: 'failed',
        timestamp: new Date(Date.now() - 30 * 60000),
        error: 'SMTP connection timeout'
      },
      {
        id: '4',
        to: 'test@example.com',
        subject: 'Test Email',
        status: 'pending',
        timestamp: new Date(Date.now() - 2 * 60000)
      }
    ];

    setTemplates(mockTemplates);
    setEmailLogs(mockLogs);
  }, []);

  const emailStats = {
    total: emailLogs.length,
    sent: emailLogs.filter(log => log.status === 'sent').length,
    failed: emailLogs.filter(log => log.status === 'failed').length,
    pending: emailLogs.filter(log => log.status === 'pending').length
  };

  const successRate = emailStats.total > 0 
    ? ((emailStats.sent / emailStats.total) * 100).toFixed(1) 
    : '0';

  const handleTestEmail = async () => {
    if (!testEmail) return;
    setIsTesting(true);
    // Simulate API call
    setTimeout(() => {
      setIsTesting(false);
      alert('Test email sent successfully!');
    }, 2000);
  };

  const getStatusConfig = (status: string) => {
    switch (status) {
      case 'sent':
        return { icon: CheckCircle2, color: 'text-success', bgColor: 'bg-success/10', ringColor: 'ring-success/20' };
      case 'failed':
        return { icon: XCircle, color: 'text-destructive', bgColor: 'bg-destructive/10', ringColor: 'ring-destructive/20' };
      case 'pending':
        return { icon: Clock, color: 'text-warning', bgColor: 'bg-warning/10', ringColor: 'ring-warning/20' };
      default:
        return { icon: Mail, color: 'text-muted-foreground', bgColor: 'bg-muted/10', ringColor: 'ring-muted/20' };
    }
  };

  return (
    <div className="space-y-6">
      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Total Sent</p>
                <p className="text-3xl font-bold text-primary">{emailStats.total}</p>
              </div>
              <Mail className="h-8 w-8 text-primary opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Delivered</p>
                <p className="text-3xl font-bold text-success">{emailStats.sent}</p>
              </div>
              <CheckCircle2 className="h-8 w-8 text-success opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Failed</p>
                <p className="text-3xl font-bold text-destructive">{emailStats.failed}</p>
              </div>
              <XCircle className="h-8 w-8 text-destructive opacity-50" />
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Success Rate</p>
                <p className="text-3xl font-bold text-success">{successRate}%</p>
              </div>
              <TrendingUp className="h-8 w-8 text-success opacity-50" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Main Content */}
      <Tabs defaultValue="configuration" className="space-y-6">
        <TabsList className="grid w-full grid-cols-3 max-w-2xl">
          <TabsTrigger value="configuration">Configuration</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="logs">Email Logs</TabsTrigger>
        </TabsList>

        {/* Configuration Tab */}
        <TabsContent value="configuration" className="space-y-6">
          <div className="grid gap-6 md:grid-cols-2">
            {/* SMTP Settings */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Server className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">SMTP Settings</CardTitle>
                    <CardDescription>Configure email server connection</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="smtp-host">SMTP Host</Label>
                  <Input
                    id="smtp-host"
                    value={config.smtpHost}
                    onChange={(e) => setConfig({ ...config, smtpHost: e.target.value })}
                    placeholder="smtp.gmail.com"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="smtp-port">SMTP Port</Label>
                  <Input
                    id="smtp-port"
                    type="number"
                    value={config.smtpPort}
                    onChange={(e) => setConfig({ ...config, smtpPort: parseInt(e.target.value) })}
                    placeholder="587"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="smtp-user">SMTP Username</Label>
                  <div className="relative">
                    <User className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="smtp-user"
                      value={config.smtpUser}
                      onChange={(e) => setConfig({ ...config, smtpUser: e.target.value })}
                      placeholder="username@example.com"
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="smtp-password">SMTP Password</Label>
                  <div className="relative">
                    <Key className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="smtp-password"
                      type={showPassword ? 'text' : 'password'}
                      value={config.smtpPassword}
                      onChange={(e) => setConfig({ ...config, smtpPassword: e.target.value })}
                      placeholder="••••••••"
                      className="pl-10 pr-10"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
                    >
                      {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </Button>
                  </div>
                </div>

                <div className="flex items-center justify-between">
                  <Label htmlFor="use-tls">Use TLS/SSL</Label>
                  <Switch
                    id="use-tls"
                    checked={config.useTls}
                    onCheckedChange={(checked) => setConfig({ ...config, useTls: checked })}
                  />
                </div>

                <div className="flex items-center justify-between">
                  <Label htmlFor="enabled">Email Service Enabled</Label>
                  <Switch
                    id="enabled"
                    checked={config.enabled}
                    onCheckedChange={(checked) => setConfig({ ...config, enabled: checked })}
                  />
                </div>

                <Button className="w-full gap-2">
                  <Settings className="h-4 w-4" />
                  Save Configuration
                </Button>
              </CardContent>
            </Card>

            {/* Sender Settings & Test */}
            <div className="space-y-6">
              <Card className="hover-lift glass-effect ring-1 ring-border/50">
                <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                  <div className="flex items-center gap-2">
                    <Send className="h-5 w-5 text-success" />
                    <div>
                      <CardTitle className="text-lg">Sender Settings</CardTitle>
                      <CardDescription>Configure default sender information</CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-6 space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="from-email">From Email</Label>
                    <Input
                      id="from-email"
                      type="email"
                      value={config.fromEmail}
                      onChange={(e) => setConfig({ ...config, fromEmail: e.target.value })}
                      placeholder="noreply@smartcollect.io"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="from-name">From Name</Label>
                    <Input
                      id="from-name"
                      value={config.fromName}
                      onChange={(e) => setConfig({ ...config, fromName: e.target.value })}
                      placeholder="SmartCollect"
                    />
                  </div>
                </CardContent>
              </Card>

              <Card className="hover-lift glass-effect ring-1 ring-border/50">
                <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                  <div className="flex items-center gap-2">
                    <TestTube className="h-5 w-5 text-warning" />
                    <div>
                      <CardTitle className="text-lg">Test Email</CardTitle>
                      <CardDescription>Send a test email to verify configuration</CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-6 space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="test-email">Recipient Email</Label>
                    <Input
                      id="test-email"
                      type="email"
                      value={testEmail}
                      onChange={(e) => setTestEmail(e.target.value)}
                      placeholder="test@example.com"
                    />
                  </div>

                  <Button 
                    onClick={handleTestEmail} 
                    disabled={!testEmail || isTesting}
                    className="w-full gap-2"
                  >
                    {isTesting ? (
                      <>
                        <Activity className="h-4 w-4 animate-spin" />
                        Sending...
                      </>
                    ) : (
                      <>
                        <Send className="h-4 w-4" />
                        Send Test Email
                      </>
                    )}
                  </Button>
                </CardContent>
              </Card>
            </div>
          </div>
        </TabsContent>

        {/* Templates Tab */}
        <TabsContent value="templates" className="space-y-4">
          <Card className="hover-lift glass-effect ring-1 ring-border/50">
            <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileText className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">Email Templates</CardTitle>
                    <CardDescription>Manage email templates for notifications</CardDescription>
                  </div>
                </div>
                <Button size="sm" className="gap-2">
                  <FileText className="h-4 w-4" />
                  New Template
                </Button>
              </div>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="space-y-3">
                {templates.map((template) => (
                  <div
                    key={template.id}
                    className="group relative rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-2">
                          <h4 className="text-sm font-semibold">{template.name}</h4>
                          <Badge variant="outline" className="text-[10px] px-2 py-0">
                            {template.type}
                          </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground mb-1">
                          <strong>Subject:</strong> {template.subject}
                        </p>
                        <p className="text-xs text-muted-foreground line-clamp-2">
                          {template.body}
                        </p>
                      </div>
                      <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                        <Settings className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Logs Tab */}
        <TabsContent value="logs" className="space-y-4">
          <Card className="hover-lift glass-effect ring-1 ring-border/50">
            <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
              <div className="flex items-center gap-2">
                <Activity className="h-5 w-5 text-primary" />
                <div>
                  <CardTitle className="text-lg">Email Logs</CardTitle>
                  <CardDescription>Recent email delivery history</CardDescription>
                </div>
              </div>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="space-y-3 max-h-[600px] overflow-y-auto scrollbar-custom pr-2">
                {emailLogs.map((log) => {
                  const statusConfig = getStatusConfig(log.status);
                  const StatusIcon = statusConfig.icon;
                  
                  return (
                    <div
                      key={log.id}
                      className="rounded-xl border border-border/50 bg-gradient-to-br from-card to-muted/20 p-4 hover-lift transition-all"
                    >
                      <div className="flex items-start gap-3">
                        <div className={`flex h-10 w-10 items-center justify-center rounded-lg flex-shrink-0 ring-1 ${statusConfig.bgColor} ${statusConfig.color} ${statusConfig.ringColor}`}>
                          <StatusIcon className="h-5 w-5" />
                        </div>
                        
                        <div className="flex-1 min-w-0">
                          <div className="flex items-start justify-between gap-2 mb-1">
                            <div className="flex-1">
                              <p className="text-sm font-semibold">{log.subject}</p>
                              <p className="text-xs text-muted-foreground">To: {log.to}</p>
                            </div>
                            <div className="flex items-center gap-2">
                              <Badge 
                                variant="outline" 
                                className={`text-[10px] px-2 py-0 ${statusConfig.bgColor} ${statusConfig.color} border-0`}
                              >
                                {log.status}
                              </Badge>
                              <span className="text-xs text-muted-foreground flex-shrink-0">
                                {log.timestamp.toLocaleTimeString()}
                              </span>
                            </div>
                          </div>
                          
                          {log.error && (
                            <div className="mt-2 rounded-lg bg-destructive/10 p-2 ring-1 ring-destructive/20">
                              <p className="text-xs text-destructive font-medium">{log.error}</p>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
