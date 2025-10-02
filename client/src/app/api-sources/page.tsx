"use client";

import { useEffect, useState } from "react";
import { Plus, RefreshCw, Trash2, Edit, Play, TestTube } from "lucide-react";
import { CreateApiSourceModal } from "@/components/CreateApiSourceModal";
import { DashboardLayout } from "@/components/DashboardLayout";
import { API_BASE } from "@/lib/api";

interface ApiSource {
  id: string;
  name: string;
  description?: string;
  apiType: string;
  endpointUrl: string;
  httpMethod: string;
  authType?: string;
  authLocation?: string;
  headerName?: string;
  queryParam?: string;
  hasApiKey?: boolean;
  enabled: boolean;
  lastRunAt?: string;
  lastUsedAt?: string;
  lastStatus?: string;
  consecutiveFailures: number;
  createdAt: string;
  updatedAt: string;
}

export default function ApiSourcesPage() {
  const [sources, setSources] = useState<ApiSource[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  useEffect(() => {
    void fetchSources();
  }, []);

  const fetchSources = async () => {
    setLoading(true);
    setErrorMessage(null);

    try {
      const response = await fetch(`${API_BASE}/api/sources`, {
        cache: "no-store",
        signal: AbortSignal.timeout(10000),
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `Request failed with status ${response.status}`);
      }

      const payload = await response.json().catch(() => {
        throw new Error("Invalid JSON received from the API.");
      });

      if (Array.isArray(payload)) {
        setSources(payload);
      } else if (Array.isArray(payload?.items)) {
        setSources(payload.items as ApiSource[]);
      } else {
        setSources([]);
        setErrorMessage("Unexpected response format received from the API.");
      }
    } catch (error) {
      console.error("Failed to fetch API sources", error);
      setSources([]);
      setErrorMessage(error instanceof Error ? error.message : "Failed to load API sources.");
    } finally {
      setLoading(false);
    }
  };

  const testConnection = async (id: string) => {
    try {
      const response = await fetch(`${API_BASE}/api/sources/${id}/test-connection`, {
        method: "POST",
        signal: AbortSignal.timeout(10000),
      });

      const result = await response.json().catch(() => ({ success: false, message: "Unable to parse response." }));

      if (!response.ok) {
        throw new Error(result?.message || `Test failed with status ${response.status}`);
      }

      window.alert(result.success ? "Connection successful." : result.message ?? "Connection failed.");
    } catch (error) {
      window.alert(`Test failed: ${error instanceof Error ? error.message : error}`);
    }
  };

  const triggerIngestion = async (id: string) => {
    try {
      const response = await fetch(`${API_BASE}/api/sources/${id}/trigger`, {
        method: "POST",
        signal: AbortSignal.timeout(15000),
      });

      const result = await response.json().catch(() => ({ success: false, error: "Unable to parse response." }));

      if (!response.ok) {
        throw new Error(result?.error || `Trigger failed with status ${response.status}`);
      }

      if (result.success) {
        window.alert(
          `Ingestion successful.\n\n` +
            `Records: ${result.recordsFetched}\n` +
            `Created: ${result.documentsCreated}\n` +
            `Failed: ${result.documentsFailed}\n` +
            `Time: ${result.executionTimeMs}ms`
        );
        void fetchSources();
      } else {
        const errorMsg = result.errorMessage || result.error || "Unknown error";
        window.alert(`Ingestion failed: ${errorMsg}`);
      }
    } catch (error) {
      window.alert(`Failed to trigger ingestion: ${error instanceof Error ? error.message : error}`);
    }
  };

  const toggleEnabled = async (source: ApiSource) => {
    try {
      const response = await fetch(`${API_BASE}/api/sources/${source.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ enabled: !source.enabled }),
        signal: AbortSignal.timeout(10000),
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `Toggle failed with status ${response.status}`);
      }

      void fetchSources();
    } catch (error) {
      console.error("Failed to toggle source", error);
      window.alert(`Failed to update source: ${error instanceof Error ? error.message : error}`);
    }
  };

  const deleteSource = async (id: string, name: string) => {
    if (!window.confirm(`Delete "${name}"?`)) return;

    try {
      const response = await fetch(`${API_BASE}/api/sources/${id}`, {
        method: "DELETE",
        signal: AbortSignal.timeout(10000),
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `Delete failed with status ${response.status}`);
      }

      void fetchSources();
    } catch (error) {
      console.error("Failed to delete source", error);
      window.alert(`Failed to delete source: ${error instanceof Error ? error.message : error}`);
    }
  };

  const getStatusColor = (status?: string) => {
    switch (status) {
      case "Success":
        return "text-green-400";
      case "Failed":
        return "text-red-400";
      default:
        return "text-gray-400";
    }
  };

  const isEmpty = !loading && sources.length === 0;

  return (
    <DashboardLayout>
      {/* Header */}
      <div className="max-w-7xl mx-auto mb-8">
        <div className="backdrop-blur-xl bg-white/5 rounded-2xl border border-white/10 p-8 shadow-2xl">
          <div className="flex items-center justify-between flex-wrap gap-4">
            <div>
              <h1 className="text-4xl font-bold bg-gradient-to-r from-blue-400 via-purple-400 to-pink-400 bg-clip-text text-transparent mb-2">
                API Sources
              </h1>
              <p className="text-gray-400 max-w-2xl">
                Manage external API connections, encrypted credentials, and ingestion diagnostics.
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={() => void fetchSources()}
                disabled={loading}
                className="px-4 py-2 bg-white/5 hover:bg-white/10 disabled:opacity-50 disabled:hover:bg-white/5 border border-white/10 rounded-lg transition-all duration-200 flex items-center gap-2"
              >
                <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
                Refresh
              </button>
              <button
                onClick={() => setShowCreateModal(true)}
                className="px-6 py-2 bg-gradient-to-r from-blue-500 to-purple-500 hover:from-blue-600 hover:to-purple-600 rounded-lg transition-all duration-200 flex items-center gap-2 font-medium shadow-lg shadow-blue-500/25"
              >
                <Plus className="w-4 h-4" />
                Add Source
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto space-y-4">
        {errorMessage && (
          <div className="bg-red-500/10 border border-red-500/30 text-red-200 rounded-xl px-4 py-3">
            {errorMessage}
          </div>
        )}

        {loading ? (
          <div className="text-center py-12 text-gray-400">Loading sources…</div>
        ) : isEmpty ? (
          <div className="backdrop-blur-xl bg-white/5 rounded-2xl border border-white/10 p-12 text-center">
            <p className="text-gray-400 mb-4">No API sources configured yet.</p>
            <button
              onClick={() => setShowCreateModal(true)}
              className="px-6 py-2 bg-gradient-to-r from-blue-500 to-purple-500 rounded-lg"
            >
              Add Your First Source
            </button>
          </div>
        ) : (
          sources.map((source) => (
            <div
              key={source.id}
              className="backdrop-blur-xl bg-white/5 rounded-2xl border border-white/10 p-6 hover:bg-white/10 transition-all duration-200 shadow-xl"
            >
              <div className="flex items-start justify-between gap-6 flex-wrap">
                <div className="flex-1 min-w-[240px]">
                  <div className="flex items-center gap-3 mb-2 flex-wrap">
                    <h3 className="text-xl font-semibold text-white">
                      {source.name}
                    </h3>
                    <span className="px-2 py-1 bg-blue-500/20 text-blue-300 text-xs rounded-md border border-blue-500/30">
                      {source.apiType}
                    </span>
                    <span className="px-2 py-1 bg-purple-500/20 text-purple-300 text-xs rounded-md border border-purple-500/30">
                      {source.httpMethod}
                    </span>
                    {source.authType && source.authType !== "None" && (
                      <span className="px-2 py-1 bg-amber-500/20 text-amber-300 text-xs rounded-md border border-amber-500/30">
                        {source.authType}
                      </span>
                    )}
                    {source.hasApiKey && (
                      <span className="px-2 py-1 bg-emerald-500/20 text-emerald-300 text-xs rounded-md border border-emerald-500/30">
                        Key set
                      </span>
                    )}
                    <label className="relative inline-flex items-center cursor-pointer">
                      <input
                        type="checkbox"
                        checked={source.enabled}
                        onChange={() => void toggleEnabled(source)}
                        className="sr-only peer"
                      />
                      <div className="w-11 h-6 bg-gray-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-green-500"></div>
                    </label>
                  </div>

                  {source.description && (
                    <p className="text-gray-400 text-sm mb-3">
                      {source.description}
                    </p>
                  )}

                  <div className="text-sm text-gray-500 font-mono mb-3 break-all">
                    {source.endpointUrl}
                  </div>

                  <div className="flex items-center gap-6 text-sm flex-wrap text-gray-300">
                    {source.lastRunAt && (
                      <div>
                        <span className="text-gray-500">Last Run: </span>
                        {new Date(source.lastRunAt).toLocaleString()}
                      </div>
                    )}
                    {source.lastUsedAt && (
                      <div>
                        <span className="text-gray-500">Key Used: </span>
                        {new Date(source.lastUsedAt).toLocaleString()}
                      </div>
                    )}
                    {source.lastStatus && (
                      <div>
                        <span className="text-gray-500">Status: </span>
                        <span className={getStatusColor(source.lastStatus)}>
                          {source.lastStatus}
                        </span>
                      </div>
                    )}
                    {source.consecutiveFailures > 0 && (
                      <div className="text-red-400">
                        {source.consecutiveFailures} consecutive failures
                      </div>
                    )}
                    {source.authType === "ApiKey" && (
                      <div>
                        Auth: {source.authLocation === "query" ? (
                          <>
                            Query param <span className="text-gray-100">{source.queryParam || "api_key"}</span>
                          </>
                        ) : (
                          <>
                            Header <span className="text-gray-100">{source.headerName || "X-API-Key"}</span>
                          </>
                        )}
                      </div>
                    )}
                  </div>
                </div>

                <div className="flex gap-2">
                  <button
                    onClick={() => void testConnection(source.id)}
                    className="p-2 bg-blue-500/20 hover:bg-blue-500/30 border border-blue-500/30 rounded-lg transition-all"
                    title="Test Connection"
                  >
                    <TestTube className="w-4 h-4 text-blue-300" />
                  </button>
                  <button
                    onClick={() => void triggerIngestion(source.id)}
                    className="p-2 bg-green-500/20 hover:bg-green-500/30 border border-green-500/30 rounded-lg transition-all"
                    title="Trigger Ingestion"
                  >
                    <Play className="w-4 h-4 text-green-300" />
                  </button>
                  <button
                    onClick={() => window.alert("Edit modal coming soon!")}
                    className="p-2 bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-all"
                    title="Edit"
                  >
                    <Edit className="w-4 h-4 text-gray-300" />
                  </button>
                  <button
                    onClick={() => void deleteSource(source.id, source.name)}
                    className="p-2 bg-red-500/20 hover:bg-red-500/30 border border-red-500/30 rounded-lg transition-all"
                    title="Delete"
                  >
                    <Trash2 className="w-4 h-4 text-red-300" />
                  </button>
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      <CreateApiSourceModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSuccess={() => void fetchSources()}
      />
    </DashboardLayout>
  );
}
