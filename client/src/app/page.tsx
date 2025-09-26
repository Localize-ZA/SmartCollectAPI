import { HealthStatus } from "@/components/HealthStatus";
import { IngestForm } from "@/components/IngestForm";
import { StatsOverview } from "@/components/StatsOverview";
import { StagingOverview } from "@/components/StagingOverview";
import { DocumentsPanel } from "@/components/DocumentsPanel";
import { ServerStatusIndicator } from "@/components/ServerStatusIndicator";

export default function Home() {
  return (
    <div className="min-h-screen w-full p-4 md:p-6 lg:p-8">
      <ServerStatusIndicator />
      <div className="mx-auto flex max-w-[1600px] flex-col gap-6">
        <header className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">SmartCollect Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            Observe pipeline health, monitor staging queues, and explore ingested documents in one place.
          </p>
        </header>
        <StatsOverview />
        <div className="grid gap-6 xl:grid-cols-4 lg:grid-cols-3">
          <div className="xl:col-span-3 lg:col-span-2 space-y-6">
            <DocumentsPanel />
          </div>
          <div className="space-y-6">
            <HealthStatus />
            <IngestForm />
          </div>
        </div>
        <StagingOverview />
      </div>
    </div>
  );
}
