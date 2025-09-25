import { HealthStatus } from "@/components/HealthStatus";
import { IngestForm } from "@/components/IngestForm";
import { StatsOverview } from "@/components/StatsOverview";
import { StagingOverview } from "@/components/StagingOverview";
import { DocumentsPanel } from "@/components/DocumentsPanel";

export default function Home() {
  return (
    <div className="min-h-screen w-full p-6 md:p-10">
      <div className="mx-auto flex max-w-6xl flex-col gap-6">
        <header className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">SmartCollect Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            Observe pipeline health, monitor staging queues, and explore ingested documents in one place.
          </p>
        </header>
        <StatsOverview />
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-6">
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
