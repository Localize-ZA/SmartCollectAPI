import { DashboardLayout } from "@/components/DashboardLayout";
import { SystemHealthVisualizer } from "@/components/SystemHealthVisualizer";

export default function HealthPage() {
  return (
    <DashboardLayout>
      <SystemHealthVisualizer />
    </DashboardLayout>
  );
}