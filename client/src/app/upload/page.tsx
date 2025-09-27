import { DashboardLayout } from "@/components/DashboardLayout";
import { IngestForm } from "@/components/IngestForm";

export default function UploadPage() {
  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div className="space-y-2">
          <h1 className="text-2xl font-bold tracking-tight">
            Document Upload
          </h1>
          <p className="text-sm text-muted-foreground">
            Upload and ingest documents into the SmartCollect system
          </p>
        </div>

        <div className="max-w-4xl">
          <IngestForm />
        </div>
      </div>
    </DashboardLayout>
  );
}