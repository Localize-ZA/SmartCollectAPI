import { DashboardLayout } from "@/components/DashboardLayout";
import { HealthStatus } from "@/components/HealthStatus";
import { StatsOverview } from "@/components/StatsOverview";
import { StagingOverview } from "@/components/StagingOverview";

export default function Home() {
  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div className="space-y-2">
          <h1 className="text-2xl font-bold tracking-tight">
            Dashboard Overview
          </h1>
          <p className="text-sm text-muted-foreground">
            Monitor your SmartCollect API system at a glance
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <HealthStatus />
          <div className="md:col-span-2">
            <StatsOverview />
          </div>
        </div>

        <div className="grid gap-4 lg:grid-cols-1">
          <StagingOverview />
        </div>
      </div>
    </DashboardLayout>
  );
}
