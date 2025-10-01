import { DashboardLayout } from "@/components/DashboardLayout";
import { MicroservicesStatus } from "@/components/MicroservicesStatus";
import { MicroservicesDetailView } from "@/components/MicroservicesDetailView";
import { Server } from "lucide-react";

export default function MicroservicesPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
            <Server className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
              Microservices
            </h1>
            <p className="text-muted-foreground mt-1">
              Monitor health, performance, and configuration of all microservices
            </p>
          </div>
        </div>

        {/* Status Overview */}
        <MicroservicesStatus />

        {/* Detailed View */}
        <MicroservicesDetailView />
      </div>
    </DashboardLayout>
  );
}