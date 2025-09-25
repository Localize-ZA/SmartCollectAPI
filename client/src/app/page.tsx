import { HealthStatus } from "@/components/HealthStatus";
import { IngestForm } from "@/components/IngestForm";

export default function Home() {
  return (
    <div className="min-h-screen w-full p-6 md:p-10">
      <div className="mx-auto max-w-4xl space-y-6">
        <header className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">SmartCollect Dashboard</h1>
          <p className="text-sm text-muted-foreground">Check server health and upload documents for processing.</p>
        </header>
        <HealthStatus />
        <IngestForm />
      </div>
    </div>
  );
}
