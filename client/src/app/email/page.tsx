import { DashboardLayout } from "@/components/DashboardLayout";
import { EmailServiceManager } from "@/components/EmailServiceManager";
import { Mail } from "lucide-react";

export default function EmailPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
            <Mail className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
              Email Service
            </h1>
            <p className="text-muted-foreground mt-1">
              Configure SMTP settings, manage email templates, and monitor delivery status
            </p>
          </div>
        </div>

        {/* Email Service Manager */}
        <EmailServiceManager />
      </div>
    </DashboardLayout>
  );
}
