"use client";

import { useState } from "react";
import { AlertCircle, FileSearch, Loader2 } from "lucide-react";
import { autoFillApiSource, ApiSourceAutoFillResult } from "@/lib/api";

interface ImportApiDocsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onApply: (result: ApiSourceAutoFillResult) => void;
}

export function ImportApiDocsModal({ isOpen, onClose, onApply }: ImportApiDocsModalProps) {
  const [docsUrl, setDocsUrl] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ApiSourceAutoFillResult | null>(null);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setResult(null);

    if (!docsUrl.trim()) {
      setError("Please provide a documentation URL.");
      return;
    }

    try {
      setLoading(true);
      const response = await autoFillApiSource(docsUrl.trim(), notes.trim() || undefined);
      setResult(response);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  const handleApply = () => {
    if (!result) return;
    onApply(result);
    setDocsUrl("");
    setNotes("");
    setResult(null);
    setError(null);
    onClose();
  };

  const handleDismiss = () => {
    setDocsUrl("");
    setNotes("");
    setResult(null);
    setError(null);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div className="w-full max-w-2xl rounded-2xl border border-white/10 bg-gray-900 text-white shadow-xl">
        <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
          <div>
            <h2 className="text-xl font-semibold">Import API from Documentation</h2>
            <p className="text-sm text-gray-400">
              Paste a HTTPS documentation link. We will scan it and prefill suggested values.
            </p>
          </div>
          <button
            onClick={handleDismiss}
            className="rounded-lg p-2 text-gray-400 hover:bg-white/10 hover:text-white"
            aria-label="Close modal"
          >
            ?
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 px-6 py-4">
          <div className="space-y-2">
            <label className="text-sm font-medium text-gray-300" htmlFor="docs-url">
              Documentation URL
            </label>
            <input
              id="docs-url"
              type="url"
              value={docsUrl}
              onChange={(e) => setDocsUrl(e.target.value)}
              placeholder="https://developer.example.com/docs"
              className="w-full rounded-lg border border-white/10 bg-gray-800 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-gray-300" htmlFor="docs-notes">
              Notes (optional)
            </label>
            <textarea
              id="docs-notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Add any hints such as environment (sandbox), preferred endpoints, or auth guidance."
              rows={3}
              className="w-full rounded-lg border border-white/10 bg-gray-800 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
          </div>

          {error && (
            <div className="flex items-start gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-200">
              <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {result && (
            <div className="space-y-3 rounded-lg border border-white/10 bg-gray-800/60 p-4">
              <h3 className="flex items-center gap-2 text-sm font-semibold text-blue-200">
                <FileSearch className="h-4 w-4" /> Suggestions Ready
              </h3>
              <div className="space-y-2 text-sm text-gray-200">
                {result.suggestions.length === 0 ? (
                  <p>No suggestions found. You may need to fill the form manually.</p>
                ) : (
                  <ul className="space-y-2">
                    {result.suggestions.map((suggestion) => (
                      <li key={suggestion.field} className="rounded border border-white/10 bg-gray-900/70 px-3 py-2">
                        <div className="flex items-center justify-between text-xs uppercase text-gray-400">
                          <span>{suggestion.field}</span>
                          <span>Confidence: {(suggestion.confidence * 100).toFixed(0)}%</span>
                        </div>
                        <div className="mt-1 text-sm text-white">{suggestion.value ?? <span className="text-gray-500">(no value)</span>}</div>
                        {suggestion.notes && (
                          <div className="mt-1 text-xs text-gray-400">{suggestion.notes}</div>
                        )}
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {result.warnings.length > 0 && (
                <div className="space-y-1 rounded border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
                  <div className="font-semibold">Warnings</div>
                  <ul className="list-disc space-y-1 pl-4">
                    {result.warnings.map((warning, index) => (
                      <li key={index}>{warning}</li>
                    ))}
                  </ul>
                </div>
              )}

              {result.sampleSnippet && (
                <div className="rounded border border-white/10 bg-black/40 px-3 py-2 text-xs font-mono text-gray-300">
                  {result.sampleSnippet}
                </div>
              )}
            </div>
          )}

          <div className="flex justify-end gap-3 border-t border-white/10 pt-4">
            <button
              type="button"
              onClick={handleDismiss}
              className="rounded-lg border border-white/10 px-4 py-2 text-sm text-gray-300 hover:bg-white/10"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white hover:bg-blue-600 disabled:cursor-not-allowed disabled:bg-blue-500/60"
            >
              {loading ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Scanning
                </>
              ) : (
                "Fetch Suggestions"
              )}
            </button>
            <button
              type="button"
              disabled={!result || loading}
              onClick={handleApply}
              className="flex items-center gap-2 rounded-lg bg-emerald-500 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-600 disabled:cursor-not-allowed disabled:bg-emerald-500/60"
            >
              Apply Suggestions
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
