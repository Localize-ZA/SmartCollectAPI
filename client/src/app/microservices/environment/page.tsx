import { DashboardLayout } from "@/components/DashboardLayout";
import { MicroserviceEnvironmentManager } from "@/components/MicroserviceEnvironmentManager";
import { Settings } from "lucide-react";

export default function MicroserviceEnvironmentPage() {
  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 shadow-lg">
            <Settings className="h-6 w-6 text-primary-foreground" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-br from-foreground to-foreground/70 bg-clip-text text-transparent">
              Microservice Environment
            </h1>
            <p className="text-muted-foreground">
              Configure environment variables and service settings
            </p>
          </div>
        </div>

        <MicroserviceEnvironmentManager />
      </div>
    </DashboardLayout>
  );
}
