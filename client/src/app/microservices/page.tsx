import { DashboardLayout } from "@/components/DashboardLayout";
import { MicroservicesStatus } from "@/components/MicroservicesStatus";

export default function MicroservicesPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        <div className="space-y-4">
          <h1 className="text-4xl font-bold tracking-tighter">
            Microservices
          </h1>
          <p className="text-xl text-muted-foreground">
            Monitor the health and status of all microservices
          </p>
        </div>

        <MicroservicesStatus />
      </div>
    </DashboardLayout>
  );
}