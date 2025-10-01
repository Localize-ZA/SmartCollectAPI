import { DashboardLayout } from "@/components/DashboardLayout";
import { AlertsManager } from "@/components/AlertsManager";
import { AlertTriangle } from "lucide-react";

export default function AlertsPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-destructive to-destructive/80 text-destructive-foreground shadow-lg">
            <AlertTriangle className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
              Alerts & Notifications
            </h1>
            <p className="text-muted-foreground mt-1">
              Monitor system alerts, configure notifications, and manage alert rules
            </p>
          </div>
        </div>

        {/* Alerts Manager */}
        <AlertsManager />
      </div>
    </DashboardLayout>
  );
}
