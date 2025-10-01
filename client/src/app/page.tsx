import { DashboardLayout } from "@/components/DashboardLayout";
import { HealthStatus } from "@/components/HealthStatus";
import { StatsOverview } from "@/components/StatsOverview";
import { StagingOverview } from "@/components/StagingOverview";
import { MicroservicesStatus } from "@/components/MicroservicesStatus";
import { Activity, TrendingUp, Zap } from "lucide-react";

export default function Home() {
  return (
    <DashboardLayout>
      <div className="space-y-8 animate-fade-in">
        {/* Hero Section */}
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-primary/20 to-primary/5 ring-1 ring-primary/10">
              <Activity className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-br from-foreground to-foreground/70 bg-clip-text text-transparent">
                Dashboard Overview
              </h1>
              <p className="text-sm text-muted-foreground mt-1 flex items-center gap-2">
                <span className="inline-flex items-center gap-1">
                  Real-time monitoring and analytics for your intelligent document processing system
                </span>
              </p>
            </div>
          </div>

          {/* Quick Stats Bar */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="flex items-center gap-3 p-4 rounded-xl glass-effect ring-1 ring-border/50 hover-lift">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-success/10">
                <Zap className="h-5 w-5 text-success" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground">System Status</p>
                <p className="text-lg font-bold text-success">Operational</p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-4 rounded-xl glass-effect ring-1 ring-border/50 hover-lift">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <Activity className="h-5 w-5 text-primary" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground">Services Running</p>
                <p className="text-lg font-bold">4/4</p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-4 rounded-xl glass-effect ring-1 ring-border/50 hover-lift">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-chart-2/10">
                <TrendingUp className="h-5 w-5 text-chart-2" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground">Performance</p>
                <p className="text-lg font-bold text-chart-2">Excellent</p>
              </div>
            </div>
          </div>
        </div>

        {/* Main Content Grid */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          <div className="animate-slide-up" style={{ animationDelay: "0.1s" }}>
            <HealthStatus />
          </div>
          <div className="md:col-span-2 animate-slide-up" style={{ animationDelay: "0.2s" }}>
            <StatsOverview />
          </div>
        </div>

        {/* Services & Activity Grid */}
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="animate-slide-up" style={{ animationDelay: "0.3s" }}>
            <MicroservicesStatus />
          </div>
          <div className="animate-slide-up" style={{ animationDelay: "0.4s" }}>
            <StagingOverview />
          </div>
        </div>

        {/* Info Footer */}
        <div className="rounded-xl bg-muted/30 p-6 ring-1 ring-border/50">
          <div className="flex items-start gap-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 flex-shrink-0">
              <Activity className="h-5 w-5 text-primary" />
            </div>
            <div className="space-y-1 flex-1">
              <h3 className="font-semibold">SmartCollect API - Intelligent Document Processing</h3>
              <p className="text-sm text-muted-foreground">
                Advanced OCR with EasyOCR (80+ languages), semantic embeddings with Sentence-Transformers (768 dimensions),
                and intelligent entity extraction powered by spaCy. Your documents are processed through state-of-the-art
                machine learning microservices for maximum accuracy and insight extraction.
              </p>
            </div>
          </div>
        </div>
      </div>
    </DashboardLayout>
  );
}
