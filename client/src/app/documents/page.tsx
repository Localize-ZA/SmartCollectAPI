import { DashboardLayout } from "@/components/DashboardLayout";
import { DocumentsPanel } from "@/components/DocumentsPanel";

export default function DocumentsPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        <div className="space-y-4">
          <h1 className="text-4xl font-bold tracking-tighter">
            Documents
          </h1>
          <p className="text-xl text-muted-foreground">
            Browse and search through processed documents
          </p>
        </div>

        <DocumentsPanel />
      </div>
    </DashboardLayout>
  );
}