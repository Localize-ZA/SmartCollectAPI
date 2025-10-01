import { DashboardLayout } from "@/components/DashboardLayout";
import { AnalyticsDashboard } from "@/components/AnalyticsDashboard";
import { BarChart3 } from "lucide-react";

export default function AnalyticsPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
            <BarChart3 className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
              Analytics Dashboard
            </h1>
            <p className="text-muted-foreground mt-1">
              Real-time insights and performance metrics
            </p>
          </div>
        </div>

        {/* Analytics Content */}
        <AnalyticsDashboard />
      </div>
    </DashboardLayout>
  );
}
