"use client";

import { useState, useEffect } from "react";
import { Plus, RefreshCw, Trash2, Edit, Play, TestTube } from "lucide-react";
import { CreateApiSourceModal } from "@/components/CreateApiSourceModal";
import { DashboardLayout } from "@/components/DashboardLayout";

interface ApiSource {
  id: string;
  name: string;
  description?: string;
  apiType: string;
  endpointUrl: string;
  httpMethod: string;
  authType?: string;
  enabled: boolean;
  lastRunAt?: string;
  lastStatus?: string;
  consecutiveFailures: number;
  createdAt: string;
  updatedAt: string;
}

export default function ApiSourcesPage() {
  const [sources, setSources] = useState<ApiSource[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);

  useEffect(() => {
    fetchSources();
  }, []);

  const fetchSources = async () => {
    try {
      setLoading(true);
      const response = await fetch("http://localhost:5082/api/sources");
      const data = await response.json();
      setSources(data);
    } catch (error) {
      console.error("Failed to fetch sources:", error);
    } finally {
      setLoading(false);
    }
  };

  const testConnection = async (id: string) => {
    try {
      const response = await fetch(
        `http://localhost:5082/api/sources/${id}/test-connection`,
        { method: "POST" }
      );
      const result = await response.json();
      alert(result.success ? "✅ Connection successful!" : `❌ ${result.message}`);
    } catch (error) {
      alert(`❌ Test failed: ${error}`);
    }
  };

  const triggerIngestion = async (id: string) => {
    try {
      const response = await fetch(
        `http://localhost:5082/api/sources/${id}/trigger`,
        { method: "POST" }
      );
      const result = await response.json();
      
      if (result.success) {
        alert(
          `✅ Ingestion successful!\n\n` +
          `Records: ${result.recordsFetched}\n` +
          `Created: ${result.documentsCreated}\n` +
          `Failed: ${result.documentsFailed}\n` +
          `Time: ${result.executionTimeMs}ms`
        );
        fetchSources(); // Refresh list
      } else {
        // Handle both errorMessage (from normal flow) and error (from exception)
        const errorMsg = result.errorMessage || result.error || 'Unknown error';
        alert(`❌ Ingestion failed: ${errorMsg}`);
      }
    } catch (error) {
      alert(`❌ Failed: ${error}`);
    }
  };

  const toggleEnabled = async (source: ApiSource) => {
    try {
      await fetch(`http://localhost:5082/api/sources/${source.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ enabled: !source.enabled }),
      });
      fetchSources();
    } catch (error) {
      console.error("Failed to toggle source:", error);
    }
  };

  const deleteSource = async (id: string, name: string) => {
    if (!confirm(`Delete "${name}"?`)) return;
    
    try {
      await fetch(`http://localhost:5082/api/sources/${id}`, {
        method: "DELETE",
      });
      fetchSources();
    } catch (error) {
      console.error("Failed to delete source:", error);
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

  return (
    <DashboardLayout>
      {/* Header */}
      <div className="max-w-7xl mx-auto mb-8">
        <div className="backdrop-blur-xl bg-white/5 rounded-2xl border border-white/10 p-8 shadow-2xl">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-4xl font-bold bg-gradient-to-r from-blue-400 via-purple-400 to-pink-400 bg-clip-text text-transparent mb-2">
                API Sources
              </h1>
              <p className="text-gray-400">
                Manage external API connections for automated data ingestion
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={fetchSources}
                className="px-4 py-2 bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-all duration-200 flex items-center gap-2"
              >
                <RefreshCw className="w-4 h-4" />
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

      {/* Sources List */}
      <div className="max-w-7xl mx-auto space-y-4">
        {loading ? (
          <div className="text-center py-12 text-gray-400">Loading...</div>
        ) : sources.length === 0 ? (
          <div className="backdrop-blur-xl bg-white/5 rounded-2xl border border-white/10 p-12 text-center">
            <p className="text-gray-400 mb-4">No API sources configured</p>
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
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-3 mb-2">
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
                        🔐 {source.authType}
                      </span>
                    )}
                    <label className="relative inline-flex items-center cursor-pointer">
                      <input
                        type="checkbox"
                        checked={source.enabled}
                        onChange={() => toggleEnabled(source)}
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

                  <div className="text-sm text-gray-500 font-mono mb-3">
                    {source.endpointUrl}
                  </div>

                  <div className="flex items-center gap-6 text-sm">
                    {source.lastRunAt && (
                      <div>
                        <span className="text-gray-500">Last Run: </span>
                        <span className="text-gray-300">
                          {new Date(source.lastRunAt).toLocaleString()}
                        </span>
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
                      <div>
                        <span className="text-red-400">
                          ⚠️ {source.consecutiveFailures} consecutive failures
                        </span>
                      </div>
                    )}
                  </div>
                </div>

                <div className="flex gap-2">
                  <button
                    onClick={() => testConnection(source.id)}
                    className="p-2 bg-blue-500/20 hover:bg-blue-500/30 border border-blue-500/30 rounded-lg transition-all"
                    title="Test Connection"
                  >
                    <TestTube className="w-4 h-4 text-blue-300" />
                  </button>
                  <button
                    onClick={() => triggerIngestion(source.id)}
                    className="p-2 bg-green-500/20 hover:bg-green-500/30 border border-green-500/30 rounded-lg transition-all"
                    title="Trigger Ingestion"
                  >
                    <Play className="w-4 h-4 text-green-300" />
                  </button>
                  <button
                    onClick={() => alert("Edit modal coming soon!")}
                    className="p-2 bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-all"
                    title="Edit"
                  >
                    <Edit className="w-4 h-4 text-gray-300" />
                  </button>
                  <button
                    onClick={() => deleteSource(source.id, source.name)}
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

      {/* Create Modal */}
      <CreateApiSourceModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSuccess={fetchSources}
      />
    </DashboardLayout>
  );
}
