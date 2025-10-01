"use client";
import React, { useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Server,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Zap,
  Clock,
  Database,
  Code,
  Terminal,
  ExternalLink,
  RefreshCw,
  Eye,
  Settings
} from "lucide-react";
import { getAllMicroservicesHealth, MicroserviceStatus } from "@/lib/api";

interface ServiceDetail {
  name: string;
  description: string;
  port: number;
  technology: string;
  features: string[];
  endpoints: { method: string; path: string; description: string }[];
}

const serviceDetails: Record<string, ServiceDetail> = {
  "spaCy NLP": {
    name: "spaCy NLP Service",
    description: "Natural Language Processing with spaCy - 300-dimensional embeddings, NER, and sentiment analysis",
    port: 5084,
    technology: "Python 3.11 + FastAPI + spaCy",
    features: [
      "300-dimensional embeddings (en_core_web_lg)",
      "Named Entity Recognition (18 entity types)",
      "Sentiment analysis with polarity scoring",
      "Part-of-speech tagging",
      "Dependency parsing"
    ],
    endpoints: [
      { method: "POST", path: "/api/v1/embeddings/generate", description: "Generate text embeddings" },
      { method: "POST", path: "/api/v1/entities/extract", description: "Extract named entities" },
      { method: "POST", path: "/api/v1/sentiment/analyze", description: "Analyze sentiment" },
      { method: "GET", path: "/health", description: "Health check" }
    ]
  },
  "EasyOCR": {
    name: "EasyOCR Service",
    description: "Deep learning-based OCR supporting 80+ languages with GPU acceleration",
    port: 5085,
    technology: "Python 3.11 + FastAPI + EasyOCR",
    features: [
      "80+ language support",
      "GPU acceleration (CUDA optional)",
      "Bounding box detection",
      "Confidence scoring",
      "Multi-language detection"
    ],
    endpoints: [
      { method: "POST", path: "/api/v1/ocr/extract", description: "Extract text from images" },
      { method: "POST", path: "/api/v1/ocr/detect", description: "Detect text regions" },
      { method: "GET", path: "/api/v1/ocr/languages", description: "List supported languages" },
      { method: "GET", path: "/health", description: "Health check" }
    ]
  },
  "Sentence-Transformers": {
    name: "Sentence-Transformers Service",
    description: "State-of-the-art sentence embeddings using all-mpnet-base-v2 (768-dimensional)",
    port: 5086,
    technology: "Python 3.11 + FastAPI + Sentence-Transformers",
    features: [
      "768-dimensional embeddings",
      "all-mpnet-base-v2 model",
      "Semantic similarity scoring",
      "Batch processing support",
      "GPU acceleration support"
    ],
    endpoints: [
      { method: "POST", path: "/api/v1/embeddings/single", description: "Generate single embedding" },
      { method: "POST", path: "/api/v1/embeddings/batch", description: "Generate batch embeddings" },
      { method: "POST", path: "/api/v1/embeddings/similarity", description: "Calculate similarity" },
      { method: "GET", path: "/health", description: "Health check" }
    ]
  }
};

export function MicroservicesDetailView() {
  const [services, setServices] = useState<MicroserviceStatus[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedService, setSelectedService] = useState<string | null>(null);

  useEffect(() => {
    fetchServices();
    const interval = setInterval(fetchServices, 30000);
    return () => clearInterval(interval);
  }, []);

  const fetchServices = async () => {
    try {
      setLoading(true);
      const data = await getAllMicroservicesHealth();
      setServices(data);
      if (!selectedService && data.length > 0) {
        setSelectedService(data[0].name);
      }
    } catch (error) {
      console.error('Failed to fetch services:', error);
    } finally {
      setLoading(false);
    }
  };

  const selectedServiceData = services.find(s => s.name === selectedService);
  const selectedServiceDetail = selectedService ? serviceDetails[selectedService] : null;

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy': return 'text-success bg-success/10 ring-success/20';
      case 'unhealthy': return 'text-destructive bg-destructive/10 ring-destructive/20';
      default: return 'text-warning bg-warning/10 ring-warning/20';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'healthy': return CheckCircle2;
      case 'unhealthy': return XCircle;
      default: return AlertCircle;
    }
  };

  return (
    <div className="space-y-6">
      {/* Service Selector */}
      <Card className="hover-lift glass-effect ring-1 ring-border/50">
        <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Eye className="h-5 w-5 text-primary" />
              <div>
                <CardTitle className="text-lg">Service Details</CardTitle>
                <CardDescription>In-depth monitoring and configuration</CardDescription>
              </div>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={fetchServices}
              disabled={loading}
            >
              <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent className="pt-6">
          <Tabs value={selectedService || undefined} onValueChange={setSelectedService}>
            <TabsList className="grid w-full grid-cols-3">
              {services.map((service) => {
                const StatusIcon = getStatusIcon(service.status);
                return (
                  <TabsTrigger key={service.name} value={service.name} className="data-[state=active]:bg-primary/10">
                    <StatusIcon className="h-4 w-4 mr-2" />
                    {service.name}
                  </TabsTrigger>
                );
              })}
            </TabsList>

            {services.map((service) => (
              <TabsContent key={service.name} value={service.name} className="space-y-6 mt-6">
                {/* Service Overview */}
                <div className="grid gap-4 md:grid-cols-3">
                  <Card className="hover-lift glass-effect ring-1 ring-border/50">
                    <CardContent className="pt-6">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Status</p>
                          <Badge className={`${getStatusColor(service.status)} border-0 text-sm`}>
                            {service.status}
                          </Badge>
                        </div>
                        <div className={`flex h-12 w-12 items-center justify-center rounded-lg ring-1 ${getStatusColor(service.status)}`}>
                          {React.createElement(getStatusIcon(service.status), { className: "h-6 w-6" })}
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  <Card className="hover-lift glass-effect ring-1 ring-border/50">
                    <CardContent className="pt-6">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Response Time</p>
                          <p className={`text-2xl font-bold ${
                            service.responseTime && service.responseTime < 1000 ? 'text-success' :
                            service.responseTime && service.responseTime < 3000 ? 'text-warning' : 'text-destructive'
                          }`}>
                            {service.responseTime || 0}ms
                          </p>
                        </div>
                        <Zap className="h-8 w-8 text-primary opacity-50" />
                      </div>
                    </CardContent>
                  </Card>

                  <Card className="hover-lift glass-effect ring-1 ring-border/50">
                    <CardContent className="pt-6">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Last Checked</p>
                          <p className="text-sm font-semibold">
                            {new Date(service.lastChecked).toLocaleTimeString()}
                          </p>
                        </div>
                        <Clock className="h-8 w-8 text-warning opacity-50" />
                      </div>
                    </CardContent>
                  </Card>
                </div>

                {/* Service Information */}
                {selectedServiceDetail && (
                  <>
                    <Card className="hover-lift glass-effect ring-1 ring-border/50">
                      <CardHeader className="bg-gradient-to-r from-card to-muted/30">
                        <div className="flex items-center gap-2">
                          <Server className="h-5 w-5 text-primary" />
                          <CardTitle className="text-lg">Service Information</CardTitle>
                        </div>
                      </CardHeader>
                      <CardContent className="pt-6 space-y-4">
                        <div>
                          <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Description</p>
                          <p className="text-sm">{selectedServiceDetail.description}</p>
                        </div>
                        
                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Endpoint</p>
                            <div className="flex items-center gap-2">
                              <code className="text-sm bg-muted/50 px-2 py-1 rounded font-mono">{service.url}</code>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => window.open(service.url, '_blank')}
                                className="h-7 w-7 p-0"
                              >
                                <ExternalLink className="h-3.5 w-3.5" />
                              </Button>
                            </div>
                          </div>
                          
                          <div>
                            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Technology</p>
                            <div className="flex items-center gap-2">
                              <Code className="h-4 w-4 text-primary" />
                              <span className="text-sm font-medium">{selectedServiceDetail.technology}</span>
                            </div>
                          </div>
                        </div>

                        {service.version && (
                          <div>
                            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Version</p>
                            <Badge variant="outline" className="font-mono">v{service.version}</Badge>
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {/* Features */}
                    <Card className="hover-lift glass-effect ring-1 ring-border/50">
                      <CardHeader className="bg-gradient-to-r from-card to-muted/30">
                        <div className="flex items-center gap-2">
                          <Settings className="h-5 w-5 text-success" />
                          <CardTitle className="text-lg">Capabilities</CardTitle>
                        </div>
                      </CardHeader>
                      <CardContent className="pt-6">
                        <ul className="space-y-2">
                          {selectedServiceDetail.features.map((feature, index) => (
                            <li key={index} className="flex items-start gap-2">
                              <CheckCircle2 className="h-4 w-4 text-success mt-0.5 flex-shrink-0" />
                              <span className="text-sm">{feature}</span>
                            </li>
                          ))}
                        </ul>
                      </CardContent>
                    </Card>

                    {/* API Endpoints */}
                    <Card className="hover-lift glass-effect ring-1 ring-border/50">
                      <CardHeader className="bg-gradient-to-r from-card to-muted/30">
                        <div className="flex items-center gap-2">
                          <Terminal className="h-5 w-5 text-warning" />
                          <CardTitle className="text-lg">API Endpoints</CardTitle>
                        </div>
                      </CardHeader>
                      <CardContent className="pt-6">
                        <div className="space-y-3">
                          {selectedServiceDetail.endpoints.map((endpoint, index) => (
                            <div key={index} className="rounded-lg bg-muted/30 p-3 ring-1 ring-border/30">
                              <div className="flex items-start gap-3">
                                <Badge 
                                  variant="outline" 
                                  className={`font-mono text-xs ${
                                    endpoint.method === 'GET' ? 'bg-primary/10 text-primary' :
                                    endpoint.method === 'POST' ? 'bg-success/10 text-success' :
                                    'bg-warning/10 text-warning'
                                  } border-0`}
                                >
                                  {endpoint.method}
                                </Badge>
                                <div className="flex-1">
                                  <code className="text-sm font-mono font-semibold">{endpoint.path}</code>
                                  <p className="text-xs text-muted-foreground mt-1">{endpoint.description}</p>
                                </div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </CardContent>
                    </Card>
                  </>
                )}

                {/* Error Display */}
                {service.error && (
                  <Card className="bg-destructive/10 ring-1 ring-destructive/20">
                    <CardHeader>
                      <div className="flex items-center gap-2">
                        <AlertCircle className="h-5 w-5 text-destructive" />
                        <CardTitle className="text-lg text-destructive">Error Details</CardTitle>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <p className="text-sm text-destructive font-mono">{service.error}</p>
                    </CardContent>
                  </Card>
                )}
              </TabsContent>
            ))}
          </Tabs>
        </CardContent>
      </Card>
    </div>
  );
}
