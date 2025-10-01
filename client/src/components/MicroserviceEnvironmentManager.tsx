"use client";
import { useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Settings,
  Save,
  RotateCcw,
  Eye,
  EyeOff,
  Server,
  Key,
  Zap,
  Database,
  Globe,
  Shield,
  Clock,
  AlertTriangle
} from "lucide-react";

interface EnvVariable {
  key: string;
  value: string;
  description: string;
  isSecret: boolean;
  isRequired: boolean;
}

interface ServiceConfig {
  name: string;
  port: number;
  endpoint: string;
  healthCheckPath: string;
  healthCheckInterval: number;
  timeout: number;
  retries: number;
  enabled: boolean;
}

export function MicroserviceEnvironmentManager() {
  const [showSecrets, setShowSecrets] = useState<{ [key: string]: boolean }>({});

  const [spacyEnv, setSpacyEnv] = useState<EnvVariable[]>([
    { key: 'SPACY_HOST', value: '0.0.0.0', description: 'Service bind address', isSecret: false, isRequired: true },
    { key: 'SPACY_PORT', value: '5084', description: 'Service port', isSecret: false, isRequired: true },
    { key: 'SPACY_MODEL', value: 'en_core_web_md', description: 'spaCy model to use', isSecret: false, isRequired: true },
    { key: 'MAX_WORKERS', value: '4', description: 'Number of worker processes', isSecret: false, isRequired: false },
    { key: 'LOG_LEVEL', value: 'INFO', description: 'Logging level', isSecret: false, isRequired: false }
  ]);

  const [ocrEnv, setOcrEnv] = useState<EnvVariable[]>([
    { key: 'OCR_HOST', value: '0.0.0.0', description: 'Service bind address', isSecret: false, isRequired: true },
    { key: 'OCR_PORT', value: '5085', description: 'Service port', isSecret: false, isRequired: true },
    { key: 'EASYOCR_LANGS', value: 'en,es,fr', description: 'Supported languages', isSecret: false, isRequired: true },
    { key: 'USE_GPU', value: 'false', description: 'Enable GPU acceleration', isSecret: false, isRequired: false },
    { key: 'GPU_DEVICE', value: '0', description: 'GPU device ID', isSecret: false, isRequired: false },
    { key: 'BATCH_SIZE', value: '8', description: 'Batch processing size', isSecret: false, isRequired: false }
  ]);

  const [embeddingsEnv, setEmbeddingsEnv] = useState<EnvVariable[]>([
    { key: 'EMBEDDINGS_HOST', value: '0.0.0.0', description: 'Service bind address', isSecret: false, isRequired: true },
    { key: 'EMBEDDINGS_PORT', value: '5086', description: 'Service port', isSecret: false, isRequired: true },
    { key: 'MODEL_NAME', value: 'sentence-transformers/all-mpnet-base-v2', description: 'Model identifier', isSecret: false, isRequired: true },
    { key: 'CACHE_DIR', value: './cache', description: 'Model cache directory', isSecret: false, isRequired: false },
    { key: 'MAX_BATCH_SIZE', value: '32', description: 'Maximum batch size', isSecret: false, isRequired: false }
  ]);

  const [serviceConfigs, setServiceConfigs] = useState<{ [key: string]: ServiceConfig }>({
    spacy: {
      name: 'spaCy NLP',
      port: 5084,
      endpoint: 'http://localhost:5084',
      healthCheckPath: '/health',
      healthCheckInterval: 30,
      timeout: 5000,
      retries: 3,
      enabled: true
    },
    ocr: {
      name: 'EasyOCR',
      port: 5085,
      endpoint: 'http://localhost:5085',
      healthCheckPath: '/health',
      healthCheckInterval: 30,
      timeout: 10000,
      retries: 3,
      enabled: true
    },
    embeddings: {
      name: 'Sentence Transformers',
      port: 5086,
      endpoint: 'http://localhost:5086',
      healthCheckPath: '/health',
      healthCheckInterval: 30,
      timeout: 5000,
      retries: 3,
      enabled: true
    }
  });

  const toggleSecret = (serviceKey: string, envKey: string) => {
    const key = `${serviceKey}-${envKey}`;
    setShowSecrets({ ...showSecrets, [key]: !showSecrets[key] });
  };

  const updateEnvValue = (
    service: 'spacy' | 'ocr' | 'embeddings',
    key: string,
    value: string
  ) => {
    const envMap = { spacy: spacyEnv, ocr: ocrEnv, embeddings: embeddingsEnv };
    const setEnvMap = { spacy: setSpacyEnv, ocr: setOcrEnv, embeddings: setEmbeddingsEnv };
    
    const updated = envMap[service].map(env =>
      env.key === key ? { ...env, value } : env
    );
    setEnvMap[service](updated);
  };

  const updateServiceConfig = (service: string, updates: Partial<ServiceConfig>) => {
    setServiceConfigs({
      ...serviceConfigs,
      [service]: { ...serviceConfigs[service], ...updates }
    });
  };

  const renderEnvVariables = (
    serviceKey: 'spacy' | 'ocr' | 'embeddings',
    envVars: EnvVariable[]
  ) => (
    <div className="space-y-4">
      {envVars.map((env) => {
        const secretKey = `${serviceKey}-${env.key}`;
        const isShown = showSecrets[secretKey];
        
        return (
          <div key={env.key} className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor={`${serviceKey}-${env.key}`} className="flex items-center gap-2">
                {env.key}
                {env.isRequired && (
                  <Badge variant="outline" className="text-[10px] px-1.5 py-0 bg-destructive/10 text-destructive border-0">
                    required
                  </Badge>
                )}
              </Label>
              {env.isSecret && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => toggleSecret(serviceKey, env.key)}
                  className="h-6 w-6 p-0"
                >
                  {isShown ? <EyeOff className="h-3 w-3" /> : <Eye className="h-3 w-3" />}
                </Button>
              )}
            </div>
            <Input
              id={`${serviceKey}-${env.key}`}
              type={env.isSecret && !isShown ? 'password' : 'text'}
              value={env.value}
              onChange={(e) => updateEnvValue(serviceKey, env.key, e.target.value)}
              placeholder={env.description}
            />
            <p className="text-xs text-muted-foreground">{env.description}</p>
          </div>
        );
      })}
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Service Status Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        {Object.entries(serviceConfigs).map(([key, config]) => (
          <Card key={key} className="hover-lift glass-effect ring-1 ring-border/50">
            <CardContent className="pt-6">
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 ring-1 ring-primary/20">
                    <Server className="h-4 w-4 text-primary" />
                  </div>
                  <div>
                    <p className="text-sm font-semibold">{config.name}</p>
                    <p className="text-xs text-muted-foreground">Port {config.port}</p>
                  </div>
                </div>
                <Switch
                  checked={config.enabled}
                  onCheckedChange={(checked) => updateServiceConfig(key, { enabled: checked })}
                />
              </div>
              <Badge variant={config.enabled ? "default" : "outline"} className="text-[10px]">
                {config.enabled ? 'Enabled' : 'Disabled'}
              </Badge>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Main Configuration */}
      <Tabs defaultValue="spacy" className="space-y-6">
        <TabsList className="grid w-full grid-cols-3 max-w-2xl">
          <TabsTrigger value="spacy">spaCy NLP</TabsTrigger>
          <TabsTrigger value="ocr">EasyOCR</TabsTrigger>
          <TabsTrigger value="embeddings">Embeddings</TabsTrigger>
        </TabsList>

        {/* spaCy Tab */}
        <TabsContent value="spacy" className="space-y-6">
          <div className="grid gap-6 lg:grid-cols-2">
            {/* Environment Variables */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Key className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">Environment Variables</CardTitle>
                    <CardDescription>Configure spaCy service variables</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6">
                {renderEnvVariables('spacy', spacyEnv)}
                <Button className="w-full mt-4 gap-2">
                  <Save className="h-4 w-4" />
                  Save Environment
                </Button>
              </CardContent>
            </Card>

            {/* Service Configuration */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Settings className="h-5 w-5 text-success" />
                  <div>
                    <CardTitle className="text-lg">Service Configuration</CardTitle>
                    <CardDescription>Advanced service settings</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="spacy-endpoint">Service Endpoint</Label>
                  <div className="relative">
                    <Globe className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="spacy-endpoint"
                      value={serviceConfigs.spacy.endpoint}
                      onChange={(e) => updateServiceConfig('spacy', { endpoint: e.target.value })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="spacy-health">Health Check Path</Label>
                  <Input
                    id="spacy-health"
                    value={serviceConfigs.spacy.healthCheckPath}
                    onChange={(e) => updateServiceConfig('spacy', { healthCheckPath: e.target.value })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="spacy-interval">Health Check Interval (seconds)</Label>
                  <div className="relative">
                    <Clock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="spacy-interval"
                      type="number"
                      value={serviceConfigs.spacy.healthCheckInterval}
                      onChange={(e) => updateServiceConfig('spacy', { healthCheckInterval: parseInt(e.target.value) })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="spacy-timeout">Request Timeout (ms)</Label>
                  <Input
                    id="spacy-timeout"
                    type="number"
                    value={serviceConfigs.spacy.timeout}
                    onChange={(e) => updateServiceConfig('spacy', { timeout: parseInt(e.target.value) })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="spacy-retries">Retry Attempts</Label>
                  <Input
                    id="spacy-retries"
                    type="number"
                    value={serviceConfigs.spacy.retries}
                    onChange={(e) => updateServiceConfig('spacy', { retries: parseInt(e.target.value) })}
                  />
                </div>

                <div className="flex gap-2">
                  <Button className="flex-1 gap-2">
                    <Save className="h-4 w-4" />
                    Save Config
                  </Button>
                  <Button variant="outline" className="gap-2">
                    <RotateCcw className="h-4 w-4" />
                    Restart
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* OCR Tab */}
        <TabsContent value="ocr" className="space-y-6">
          <div className="grid gap-6 lg:grid-cols-2">
            {/* Environment Variables */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Key className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">Environment Variables</CardTitle>
                    <CardDescription>Configure EasyOCR service variables</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6">
                {renderEnvVariables('ocr', ocrEnv)}
                <div className="mt-4 rounded-lg bg-warning/10 p-3 ring-1 ring-warning/20">
                  <div className="flex gap-2">
                    <AlertTriangle className="h-4 w-4 text-warning flex-shrink-0 mt-0.5" />
                    <p className="text-xs text-warning">
                      GPU acceleration requires CUDA-compatible hardware and drivers
                    </p>
                  </div>
                </div>
                <Button className="w-full mt-4 gap-2">
                  <Save className="h-4 w-4" />
                  Save Environment
                </Button>
              </CardContent>
            </Card>

            {/* Service Configuration */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Settings className="h-5 w-5 text-success" />
                  <div>
                    <CardTitle className="text-lg">Service Configuration</CardTitle>
                    <CardDescription>Advanced service settings</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="ocr-endpoint">Service Endpoint</Label>
                  <div className="relative">
                    <Globe className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="ocr-endpoint"
                      value={serviceConfigs.ocr.endpoint}
                      onChange={(e) => updateServiceConfig('ocr', { endpoint: e.target.value })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="ocr-health">Health Check Path</Label>
                  <Input
                    id="ocr-health"
                    value={serviceConfigs.ocr.healthCheckPath}
                    onChange={(e) => updateServiceConfig('ocr', { healthCheckPath: e.target.value })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="ocr-interval">Health Check Interval (seconds)</Label>
                  <div className="relative">
                    <Clock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="ocr-interval"
                      type="number"
                      value={serviceConfigs.ocr.healthCheckInterval}
                      onChange={(e) => updateServiceConfig('ocr', { healthCheckInterval: parseInt(e.target.value) })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="ocr-timeout">Request Timeout (ms)</Label>
                  <Input
                    id="ocr-timeout"
                    type="number"
                    value={serviceConfigs.ocr.timeout}
                    onChange={(e) => updateServiceConfig('ocr', { timeout: parseInt(e.target.value) })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="ocr-retries">Retry Attempts</Label>
                  <Input
                    id="ocr-retries"
                    type="number"
                    value={serviceConfigs.ocr.retries}
                    onChange={(e) => updateServiceConfig('ocr', { retries: parseInt(e.target.value) })}
                  />
                </div>

                <div className="flex gap-2">
                  <Button className="flex-1 gap-2">
                    <Save className="h-4 w-4" />
                    Save Config
                  </Button>
                  <Button variant="outline" className="gap-2">
                    <RotateCcw className="h-4 w-4" />
                    Restart
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* Embeddings Tab */}
        <TabsContent value="embeddings" className="space-y-6">
          <div className="grid gap-6 lg:grid-cols-2">
            {/* Environment Variables */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Key className="h-5 w-5 text-primary" />
                  <div>
                    <CardTitle className="text-lg">Environment Variables</CardTitle>
                    <CardDescription>Configure embeddings service variables</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6">
                {renderEnvVariables('embeddings', embeddingsEnv)}
                <Button className="w-full mt-4 gap-2">
                  <Save className="h-4 w-4" />
                  Save Environment
                </Button>
              </CardContent>
            </Card>

            {/* Service Configuration */}
            <Card className="hover-lift glass-effect ring-1 ring-border/50">
              <CardHeader className="bg-gradient-to-r from-card to-muted/30 rounded-t-xl">
                <div className="flex items-center gap-2">
                  <Settings className="h-5 w-5 text-success" />
                  <div>
                    <CardTitle className="text-lg">Service Configuration</CardTitle>
                    <CardDescription>Advanced service settings</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="embeddings-endpoint">Service Endpoint</Label>
                  <div className="relative">
                    <Globe className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="embeddings-endpoint"
                      value={serviceConfigs.embeddings.endpoint}
                      onChange={(e) => updateServiceConfig('embeddings', { endpoint: e.target.value })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="embeddings-health">Health Check Path</Label>
                  <Input
                    id="embeddings-health"
                    value={serviceConfigs.embeddings.healthCheckPath}
                    onChange={(e) => updateServiceConfig('embeddings', { healthCheckPath: e.target.value })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="embeddings-interval">Health Check Interval (seconds)</Label>
                  <div className="relative">
                    <Clock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="embeddings-interval"
                      type="number"
                      value={serviceConfigs.embeddings.healthCheckInterval}
                      onChange={(e) => updateServiceConfig('embeddings', { healthCheckInterval: parseInt(e.target.value) })}
                      className="pl-10"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="embeddings-timeout">Request Timeout (ms)</Label>
                  <Input
                    id="embeddings-timeout"
                    type="number"
                    value={serviceConfigs.embeddings.timeout}
                    onChange={(e) => updateServiceConfig('embeddings', { timeout: parseInt(e.target.value) })}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="embeddings-retries">Retry Attempts</Label>
                  <Input
                    id="embeddings-retries"
                    type="number"
                    value={serviceConfigs.embeddings.retries}
                    onChange={(e) => updateServiceConfig('embeddings', { retries: parseInt(e.target.value) })}
                  />
                </div>

                <div className="flex gap-2">
                  <Button className="flex-1 gap-2">
                    <Save className="h-4 w-4" />
                    Save Config
                  </Button>
                  <Button variant="outline" className="gap-2">
                    <RotateCcw className="h-4 w-4" />
                    Restart
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
