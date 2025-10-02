"use client";

import { useState, useEffect } from "react";
import { DashboardLayout } from "@/components/DashboardLayout";
import { Globe, Sparkles, Languages, AlertCircle, CheckCircle2 } from "lucide-react";
import { detectLanguage, getSupportedLanguages, type LanguageDetectionResult, type SupportedLanguage } from "@/lib/api";

export default function LanguageDetectionPage() {
  const [text, setText] = useState("");
  const [minConfidence, setMinConfidence] = useState(0.5);
  const [result, setResult] = useState<LanguageDetectionResult | null>(null);
  const [supportedLanguages, setSupportedLanguages] = useState<SupportedLanguage[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingLanguages, setLoadingLanguages] = useState(true);
  const [error, setError] = useState("");
  const [serviceAvailable, setServiceAvailable] = useState(false);

  useEffect(() => {
    // Check if service is available and load supported languages
    const checkService = async () => {
      try {
        const languages = await getSupportedLanguages();
        setSupportedLanguages(languages);
        setServiceAvailable(true);
      } catch (err) {
        setError("Language detection service is not available. Please ensure the microservice is running on port 8004.");
        setServiceAvailable(false);
      } finally {
        setLoadingLanguages(false);
      }
    };

    void checkService();
  }, []);

  const sampleTexts = [
    { lang: "English", text: "Machine learning is revolutionizing the way we process documents." },
    { lang: "Spanish", text: "La inteligencia artificial está transformando nuestro mundo." },
    { lang: "French", text: "Le traitement automatique du langage naturel est fascinant." },
    { lang: "German", text: "Die künstliche Intelligenz verändert unsere Arbeitsweise." },
    { lang: "Italian", text: "L'elaborazione del linguaggio naturale è incredibilmente potente." },
    { lang: "Russian", text: "Обработка естественного языка открывает новые возможности." },
    { lang: "Portuguese", text: "O processamento de linguagem natural está em rápida evolução." },
    { lang: "Japanese", text: "自然言語処理は急速に進化しています。" }
  ];

  const handleDetect = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return;

    setLoading(true);
    setError("");
    setResult(null);

    try {
      const detection = await detectLanguage(text, minConfidence);
      setResult(detection);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Detection failed");
    } finally {
      setLoading(false);
    }
  };

  const loadSampleText = (sampleText: string) => {
    setText(sampleText);
    setResult(null);
  };

  return (
    <DashboardLayout>
      <div className="space-y-6 animate-fade-in">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-primary/20 to-primary/5 ring-1 ring-primary/10">
            <Globe className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">
              Language Detection
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Detect language from text with 75+ languages supported (Phase 4)
            </p>
          </div>
        </div>

        {/* Service Status */}
        {!loadingLanguages && (
          <div className={`glass-effect rounded-xl p-4 ring-1 ${
            serviceAvailable 
              ? 'ring-success/50 bg-success/5' 
              : 'ring-destructive/50 bg-destructive/5'
          }`}>
            <div className="flex items-center gap-3">
              {serviceAvailable ? (
                <>
                  <CheckCircle2 className="h-5 w-5 text-success" />
                  <div>
                    <p className="font-medium text-success">Service Online</p>
                    <p className="text-sm text-muted-foreground">
                      {supportedLanguages.length} languages available
                    </p>
                  </div>
                </>
              ) : (
                <>
                  <AlertCircle className="h-5 w-5 text-destructive" />
                  <div>
                    <p className="font-medium text-destructive">Service Offline</p>
                    <p className="text-sm text-muted-foreground">
                      Start the language detection microservice to use this feature
                    </p>
                  </div>
                </>
              )}
            </div>
          </div>
        )}

        {/* Detection Form */}
        <div className="glass-effect rounded-xl p-6 ring-1 ring-border/50">
          <form onSubmit={handleDetect} className="space-y-4">
            <div>
              <label className="block text-sm font-medium mb-2">
                Text to Analyze
              </label>
              <textarea
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="Enter text in any language..."
                rows={6}
                className="w-full px-4 py-3 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary focus:border-transparent resize-none"
              />
              <p className="text-xs text-muted-foreground mt-2">
                {text.length} characters
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium mb-2">
                Minimum Confidence: {minConfidence.toFixed(2)}
              </label>
              <input
                type="range"
                value={minConfidence}
                onChange={(e) => setMinConfidence(parseFloat(e.target.value))}
                min="0"
                max="1"
                step="0.05"
                className="w-full"
              />
            </div>

            <button
              type="submit"
              disabled={loading || !text.trim() || !serviceAvailable}
              className="w-full px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 font-medium"
            >
              {loading ? (
                <>
                  <div className="h-5 w-5 border-2 border-current border-t-transparent rounded-full animate-spin" />
                  Detecting...
                </>
              ) : (
                <>
                  <Sparkles className="h-5 w-5" />
                  Detect Language
                </>
              )}
            </button>
          </form>
        </div>

        {/* Sample Texts */}
        <div className="glass-effect rounded-xl p-6 ring-1 ring-border/50">
          <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Languages className="h-5 w-5 text-primary" />
            Sample Texts
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {sampleTexts.map((sample, index) => (
              <button
                key={index}
                onClick={() => loadSampleText(sample.text)}
                className="text-left p-3 rounded-lg border border-border hover:bg-accent hover:border-primary transition-colors"
              >
                <p className="text-xs font-medium text-primary mb-1">{sample.lang}</p>
                <p className="text-sm text-muted-foreground line-clamp-2">{sample.text}</p>
              </button>
            ))}
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="glass-effect rounded-xl p-4 ring-1 ring-destructive/50 bg-destructive/10">
            <p className="text-destructive font-medium">{error}</p>
          </div>
        )}

        {/* Results */}
        {result && (
          <div className="space-y-4">
            <div className="glass-effect rounded-xl p-6 ring-1 ring-border/50">
              <h3 className="text-lg font-semibold mb-4">Detection Results</h3>
              
              {/* Primary Result */}
              <div className="mb-6">
                <div className="flex items-center justify-between mb-2">
                  <h4 className="font-medium">Detected Language</h4>
                  <span className="px-3 py-1 rounded-full bg-primary/10 text-primary text-sm font-medium">
                    {(result.detectedLanguage.confidence * 100).toFixed(1)}% confidence
                  </span>
                </div>
                <div className="p-4 rounded-lg bg-accent">
                  <p className="text-2xl font-bold mb-1">
                    {result.detectedLanguage.languageName}
                  </p>
                  <div className="flex items-center gap-3 text-sm text-muted-foreground">
                    {result.detectedLanguage.isoCode639_1 && (
                      <span>ISO 639-1: {result.detectedLanguage.isoCode639_1}</span>
                    )}
                    {result.detectedLanguage.isoCode639_3 && (
                      <>
                        <span>•</span>
                        <span>ISO 639-3: {result.detectedLanguage.isoCode639_3}</span>
                      </>
                    )}
                  </div>
                </div>
              </div>

              {/* All Candidates */}
              {result.allCandidates.length > 1 && (
                <div>
                  <h4 className="font-medium mb-3">Alternative Candidates</h4>
                  <div className="space-y-2">
                    {result.allCandidates.slice(1, 6).map((candidate, index) => (
                      <div
                        key={index}
                        className="flex items-center justify-between p-3 rounded-lg border border-border"
                      >
                        <div>
                          <p className="font-medium">{candidate.languageName}</p>
                          <p className="text-xs text-muted-foreground">
                            {candidate.isoCode639_1 || candidate.isoCode639_3}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          <div className="w-32 h-2 bg-muted rounded-full overflow-hidden">
                            <div
                              className="h-full bg-primary/60"
                              style={{ width: `${candidate.confidence * 100}%` }}
                            />
                          </div>
                          <span className="text-sm text-muted-foreground w-12 text-right">
                            {(candidate.confidence * 100).toFixed(1)}%
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Stats */}
              <div className="mt-6 pt-4 border-t border-border flex items-center gap-4 text-sm text-muted-foreground">
                <span>Text length: {result.textLength} characters</span>
                <span>•</span>
                <span>{result.allCandidates.length} candidates analyzed</span>
              </div>
            </div>
          </div>
        )}

        {/* Supported Languages (Collapsed by default) */}
        {supportedLanguages.length > 0 && (
          <details className="glass-effect rounded-xl p-6 ring-1 ring-border/50">
            <summary className="cursor-pointer font-semibold text-lg mb-4">
              Supported Languages ({supportedLanguages.length})
            </summary>
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 mt-4">
              {supportedLanguages.map((lang, index) => (
                <div
                  key={index}
                  className="p-2 rounded-lg border border-border text-sm"
                >
                  <p className="font-medium">{lang.name}</p>
                  <p className="text-xs text-muted-foreground">
                    {lang.isoCode639_1 || lang.isoCode639_3}
                  </p>
                </div>
              ))}
            </div>
          </details>
        )}
      </div>
    </DashboardLayout>
  );
}
