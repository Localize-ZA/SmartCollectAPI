import { DashboardLayout } from "@/components/DashboardLayout";
import { StagingOverview } from "@/components/StagingOverview";

export default function StagingPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        <div className="space-y-4">
          <h1 className="text-4xl font-bold tracking-tighter">
            Staging Queue
          </h1>
          <p className="text-xl text-muted-foreground">
            Monitor documents in the processing pipeline
          </p>
        </div>

        <StagingOverview />
      </div>
    </DashboardLayout>
  );
}