import { DashboardLayout } from "@/components/DashboardLayout";
import { IngestForm } from "@/components/IngestForm";
import { FileText, Zap, Brain, CheckCircle } from "lucide-react";

export default function UploadPage() {
  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Header Section */}
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-lg">
              <FileText className="h-6 w-6" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
                Document Upload
              </h1>
              <p className="text-muted-foreground mt-1">
                Upload and ingest documents into the SmartCollect ML pipeline
              </p>
            </div>
          </div>

          {/* Feature Cards */}
          <div className="grid gap-3 md:grid-cols-3 mt-6">
            <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50">
              <div className="flex items-start gap-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Zap className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-sm font-semibold mb-1">Fast Processing</h3>
                  <p className="text-xs text-muted-foreground">
                    Bulk upload with concurrent processing for maximum speed
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50">
              <div className="flex items-start gap-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-success/10 text-success">
                  <Brain className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-sm font-semibold mb-1">AI-Powered</h3>
                  <p className="text-xs text-muted-foreground">
                    OCR, NER, embeddings, and semantic analysis
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-xl bg-gradient-to-br from-card to-muted/20 p-4 ring-1 ring-border/50">
              <div className="flex items-start gap-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-warning/10 text-warning">
                  <CheckCircle className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-sm font-semibold mb-1">Multiple Formats</h3>
                  <p className="text-xs text-muted-foreground">
                    JSON, XML, CSV, TXT, PDF, DOC, DOCX, MD
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Upload Form */}
        <div className="max-w-4xl">
          <IngestForm />
        </div>
      </div>
    </DashboardLayout>
  );
}