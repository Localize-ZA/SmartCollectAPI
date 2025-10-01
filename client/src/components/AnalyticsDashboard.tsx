"use client";
import { useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
  FileText,
  Layers,
  Clock,
  TrendingUp,
  Activity,
  RefreshCw,
  Download,
  Calendar,
  Zap,
  Database,
  Brain
} from "lucide-react";
import { getProcessingStats, getStagingDocuments } from "@/lib/api";

interface ProcessingTrend {
  date: string;
  uploads: number;
  processed: number;
  failed: number;
}

interface DocumentTypeStats {
  type: string;
  count: number;
  color: string;
}

const CHART_COLORS = {
  primary: 'oklch(0.55 0.22 264)',
  success: 'oklch(0.65 0.20 145)',
  warning: 'oklch(0.75 0.15 85)',
  destructive: 'oklch(0.60 0.22 25)',
  muted: 'oklch(0.50 0.02 264)'
};

export function AnalyticsDashboard() {
  const [stats, setStats] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [timeRange, setTimeRange] = useState<'24h' | '7d' | '30d'>('7d');

  // Mock trend data (would come from API in production)
  const trendData: ProcessingTrend[] = [
    { date: 'Mon', uploads: 45, processed: 42, failed: 3 },
    { date: 'Tue', uploads: 52, processed: 50, failed: 2 },
    { date: 'Wed', uploads: 61, processed: 58, failed: 3 },
    { date: 'Thu', uploads: 48, processed: 46, failed: 2 },
    { date: 'Fri', uploads: 73, processed: 70, failed: 3 },
    { date: 'Sat', uploads: 38, processed: 37, failed: 1 },
    { date: 'Sun', uploads: 42, processed: 40, failed: 2 },
  ];

  const documentTypeData: DocumentTypeStats[] = [
    { type: 'PDF', count: 125, color: CHART_COLORS.primary },
    { type: 'TXT', count: 89, color: CHART_COLORS.success },
    { type: 'JSON', count: 67, color: CHART_COLORS.warning },
    { type: 'CSV', count: 45, color: 'oklch(0.60 0.25 300)' },
    { type: 'Other', count: 34, color: CHART_COLORS.muted },
  ];

  const performanceData = [
    { name: 'OCR', avgTime: 1250, maxTime: 3500 },
    { name: 'NER', avgTime: 850, maxTime: 2100 },
    { name: 'Embeddings', avgTime: 1680, maxTime: 4200 },
    { name: 'Full Pipeline', avgTime: 3780, maxTime: 9800 },
  ];

  useEffect(() => {
    fetchStats();
    const interval = setInterval(fetchStats, 30000); // Refresh every 30s
    return () => clearInterval(interval);
  }, []);

  const fetchStats = async () => {
    try {
      setLoading(true);
      const data = await getProcessingStats();
      setStats(data);
    } catch (error) {
      console.error('Failed to fetch stats:', error);
    } finally {
      setLoading(false);
    }
  };

  const totalDocuments = stats?.totalDocuments || 0;
  const withEmbeddings = stats?.documentsWithEmbeddings || 0;
  const processedToday = stats?.processedToday || 0;
  const successRate = totalDocuments > 0 ? ((withEmbeddings / totalDocuments) * 100).toFixed(1) : '0';

  return (
    <div className="space-y-6">
      {/* Top Actions */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button
            variant={timeRange === '24h' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTimeRange('24h')}
          >
            24 Hours
          </Button>
          <Button
            variant={timeRange === '7d' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTimeRange('7d')}
          >
            7 Days
          </Button>
          <Button
            variant={timeRange === '30d' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTimeRange('30d')}
          >
            30 Days
          </Button>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={fetchStats}
            disabled={loading}
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button variant="outline" size="sm">
            <Download className="h-4 w-4 mr-2" />
            Export
          </Button>
        </div>
      </div>

      {/* Key Metrics Grid */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Documents</CardTitle>
            <Database className="h-4 w-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold text-primary">{totalDocuments}</div>
            <p className="text-xs text-muted-foreground mt-1">
              <TrendingUp className="inline h-3 w-3 text-success mr-1" />
              +12% from last period
            </p>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">With Embeddings</CardTitle>
            <Brain className="h-4 w-4 text-success" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold text-success">{withEmbeddings}</div>
            <p className="text-xs text-muted-foreground mt-1">
              {successRate}% processing success rate
            </p>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Processed Today</CardTitle>
            <Calendar className="h-4 w-4 text-warning" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold text-warning">{processedToday}</div>
            <p className="text-xs text-muted-foreground mt-1">
              <Activity className="inline h-3 w-3 mr-1" />
              Active processing
            </p>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Processing Time</CardTitle>
            <Zap className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">3.8s</div>
            <p className="text-xs text-muted-foreground mt-1">
              Per document pipeline time
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Charts Row 1 */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Upload Trend */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg">Upload Trend</CardTitle>
                <CardDescription>Document uploads over time</CardDescription>
              </div>
              <TrendingUp className="h-5 w-5 text-primary" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={trendData}>
                  <defs>
                    <linearGradient id="colorUploads" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor={CHART_COLORS.primary} stopOpacity={0.3}/>
                      <stop offset="95%" stopColor={CHART_COLORS.primary} stopOpacity={0}/>
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis dataKey="date" fontSize={12} />
                  <YAxis fontSize={12} />
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: 'oklch(1 0.01 264)', 
                      border: '1px solid oklch(0.90 0.01 264)',
                      borderRadius: '8px'
                    }}
                  />
                  <Area 
                    type="monotone" 
                    dataKey="uploads" 
                    stroke={CHART_COLORS.primary}
                    fillOpacity={1}
                    fill="url(#colorUploads)"
                    strokeWidth={2}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Document Types */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg">Document Types</CardTitle>
                <CardDescription>Distribution by file format</CardDescription>
              </div>
              <FileText className="h-5 w-5 text-success" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={documentTypeData}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ type, percent }) => `${type} ${(percent * 100).toFixed(0)}%`}
                    outerRadius={90}
                    fill="#8884d8"
                    dataKey="count"
                  >
                    {documentTypeData.map((entry, index) => (
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

      {/* Charts Row 2 */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Processing Success/Failure */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg">Processing Results</CardTitle>
                <CardDescription>Success vs failures over time</CardDescription>
              </div>
              <Layers className="h-5 w-5 text-warning" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={trendData}>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis dataKey="date" fontSize={12} />
                  <YAxis fontSize={12} />
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: 'oklch(1 0.01 264)', 
                      border: '1px solid oklch(0.90 0.01 264)',
                      borderRadius: '8px'
                    }}
                  />
                  <Legend />
                  <Bar dataKey="processed" fill={CHART_COLORS.success} name="Processed" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="failed" fill={CHART_COLORS.destructive} name="Failed" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Performance Metrics */}
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg">Service Performance</CardTitle>
                <CardDescription>Average processing times (ms)</CardDescription>
              </div>
              <Zap className="h-5 w-5 text-primary" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-[300px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={performanceData} layout="horizontal">
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis type="number" fontSize={12} />
                  <YAxis dataKey="name" type="category" fontSize={12} width={100} />
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: 'oklch(1 0.01 264)', 
                      border: '1px solid oklch(0.90 0.01 264)',
                      borderRadius: '8px'
                    }}
                  />
                  <Legend />
                  <Bar dataKey="avgTime" fill={CHART_COLORS.primary} name="Avg Time" radius={[0, 4, 4, 0]} />
                  <Bar dataKey="maxTime" fill={CHART_COLORS.warning} name="Max Time" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Bottom Stats Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <CardTitle className="text-sm font-medium">Storage Usage</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Documents</span>
                <span className="text-sm font-semibold">2.4 GB</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Embeddings</span>
                <span className="text-sm font-semibold">856 MB</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Total</span>
                <span className="text-sm font-bold text-primary">3.2 GB</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <CardTitle className="text-sm font-medium">Processing Queue</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Badge variant="outline" className="bg-warning/10 text-warning ring-warning/20">
                  Pending
                </Badge>
                <span className="text-sm font-semibold">{stats?.stagingStatus?.pending || 0}</span>
              </div>
              <div className="flex items-center justify-between">
                <Badge variant="outline" className="bg-primary/10 text-primary ring-primary/20">
                  Processing
                </Badge>
                <span className="text-sm font-semibold">{stats?.stagingStatus?.processing || 0}</span>
              </div>
              <div className="flex items-center justify-between">
                <Badge variant="outline" className="bg-success/10 text-success ring-success/20">
                  Completed
                </Badge>
                <span className="text-sm font-semibold">{stats?.stagingStatus?.completed || 0}</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="hover-lift glass-effect ring-1 ring-border/50">
          <CardHeader>
            <CardTitle className="text-sm font-medium">System Health</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">API Status</span>
                <Badge className="bg-success/10 text-success ring-success/20">Healthy</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Services</span>
                <Badge className="bg-success/10 text-success ring-success/20">3/3 Up</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Uptime</span>
                <span className="text-sm font-semibold">99.8%</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
