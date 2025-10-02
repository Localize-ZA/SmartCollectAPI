"use client";

import { useEffect, useMemo, useState } from "react";
import { X, Eye, EyeOff, AlertCircle, Lock } from "lucide-react";
import { API_BASE } from "@/lib/api";

type CreateApiSourceFormState = {
  name: string;
  description: string;
  apiType: string;
  endpointUrl: string;
  httpMethod: string;
  authType: string;
  customHeaders: string;
  requestBody: string;
  queryParams: string;
  responsePath: string;
  fieldMappings: string;
  scheduleCron: string;
  enabled: boolean;
};

const defaultFormState: CreateApiSourceFormState = {
  name: "",
  description: "",
  apiType: "REST",
  endpointUrl: "",
  httpMethod: "GET",
  authType: "None",
  customHeaders: "",
  requestBody: "",
  queryParams: "",
  responsePath: "$",
  fieldMappings: "",
  scheduleCron: "",
  enabled: false,
};

interface CreateApiSourceModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialValues?: Partial<CreateApiSourceFormState>;
  warnings?: string[];
  uncertainFields?: string[];
}

interface AuthConfig {
  [key: string]: string;
}

export function CreateApiSourceModal({
  isOpen,
  onClose,
  onSuccess,
}: CreateApiSourceModalProps) {
  const [formData, setFormData] = useState({
    name: "",
    description: "",
    apiType: "REST",
    endpointUrl: "",
    httpMethod: "GET",
    authType: "None",
    customHeaders: "",
    requestBody: "",
    queryParams: "",
    responsePath: "$",
    fieldMappings: "",
    scheduleCron: "",
    enabled: false,
  });

  const [authConfig, setAuthConfig] = useState<AuthConfig>({});
  const [showCredentials, setShowCredentials] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError("");

    try {
      const payload: Record<string, unknown> = {
        ...formData,
      };

      const authConfigPayload = { ...authConfig };
      let authConfigForPayload: Record<string, string> | undefined;

      if (formData.authType === "ApiKey") {
        payload.authLocation = authConfigPayload.in ?? "header";
        payload.headerName = authConfigPayload.header ?? "X-API-Key";
        payload.queryParam = authConfigPayload.param ?? "api_key";

        const { key: extractedKey, ...rest } = authConfigPayload;
        if (extractedKey) {
          payload.apiKey = extractedKey;
        }
        authConfigForPayload = rest;
      } else if (Object.keys(authConfigPayload).length > 0) {
        authConfigForPayload = authConfigPayload;
      }

      if (authConfigForPayload && Object.keys(authConfigForPayload).length > 0) {
        payload.authConfig = authConfigForPayload;
      }

      // Clear sensitive key from state immediately after preparing payload
      if (authConfig.key) {
        setAuthConfig((prev) => {
          const next = { ...prev };
          delete next.key;
          return next;
        });
      }

      Object.keys(payload).forEach((key) => {
        const value = payload[key];
        if (typeof value === "string" && value.trim() === "") {
          delete payload[key];
        }
      });

      const response = await fetch(`${API_BASE}/api/sources`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        signal: AbortSignal.timeout(10000),
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || "Failed to create source");
      }

      // Success!
      onSuccess();
      onClose();
      resetForm();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setFormData({
      name: "",
      description: "",
      apiType: "REST",
      endpointUrl: "",
      httpMethod: "GET",
      authType: "None",
      customHeaders: "",
      requestBody: "",
      queryParams: "",
      responsePath: "$",
      fieldMappings: "",
      scheduleCron: "",
      enabled: false,
    });
    setAuthConfig({});
    setError("");
  };

  const renderAuthFields = () => {
    switch (formData.authType) {
      case "Basic":
        return (
          <>
            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Username
              </label>
              <input
                type="text"
                value={authConfig.username || ""}
                onChange={(e) =>
                  setAuthConfig({ ...authConfig, username: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Password
              </label>
              <div className="relative">
                <input
                  type={showCredentials ? "text" : "password"}
                  value={authConfig.password || ""}
                  onChange={(e) =>
                    setAuthConfig({ ...authConfig, password: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent pr-10"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowCredentials(!showCredentials)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-200"
                >
                  {showCredentials ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
            </div>
          </>
        );

      case "Bearer":
        return (
          <div>
            <label className="block text-sm font-medium mb-2 text-gray-300">
              Bearer Token
            </label>
            <div className="relative">
              <input
                type={showCredentials ? "text" : "password"}
                value={authConfig.token || ""}
                onChange={(e) =>
                  setAuthConfig({ ...authConfig, token: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent pr-10"
                placeholder="your-bearer-token-here"
                required
              />
              <button
                type="button"
                onClick={() => setShowCredentials(!showCredentials)}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-200"
              >
                {showCredentials ? (
                  <EyeOff className="w-4 h-4" />
                ) : (
                  <Eye className="w-4 h-4" />
                )}
              </button>
            </div>
          </div>
        );

      case "ApiKey":
        return (
          <>
            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                API Key
              </label>
              <div className="relative">
                <input
                  type={showCredentials ? "text" : "password"}
                  value={authConfig.key || ""}
                  onChange={(e) =>
                    setAuthConfig({ ...authConfig, key: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent pr-10"
                  placeholder="your-api-key"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowCredentials(!showCredentials)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-200"
                >
                  {showCredentials ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Location
              </label>
              <select
                value={authConfig.in || "header"}
                onChange={(e) =>
                  setAuthConfig({ ...authConfig, in: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="header">Header</option>
                <option value="query">Query Parameter</option>
              </select>
            </div>
            {authConfig.in === "header" ? (
              <div>
                <label className="block text-sm font-medium mb-2 text-gray-300">
                  Header Name
                </label>
                <input
                  type="text"
                  value={authConfig.header || "X-API-Key"}
                  onChange={(e) =>
                    setAuthConfig({ ...authConfig, header: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="X-API-Key"
                />
              </div>
            ) : (
              <div>
                <label className="block text-sm font-medium mb-2 text-gray-300">
                  Parameter Name
                </label>
                <input
                  type="text"
                  value={authConfig.param || "api_key"}
                  onChange={(e) =>
                    setAuthConfig({ ...authConfig, param: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="api_key"
                />
              </div>
            )}
          </>
        );

      case "OAuth2":
        return (
          <div>
            <label className="block text-sm font-medium mb-2 text-gray-300">
              Access Token
            </label>
            <div className="relative">
              <input
                type={showCredentials ? "text" : "password"}
                value={authConfig.access_token || ""}
                onChange={(e) =>
                  setAuthConfig({ ...authConfig, access_token: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent pr-10"
                placeholder="your-oauth2-access-token"
                required
              />
              <button
                type="button"
                onClick={() => setShowCredentials(!showCredentials)}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-200"
              >
                {showCredentials ? (
                  <EyeOff className="w-4 h-4" />
                ) : (
                  <Eye className="w-4 h-4" />
                )}
              </button>
            </div>
            <p className="text-xs text-gray-500 mt-1">
              Note: Token refresh not yet supported. Use a long-lived token.
            </p>
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-gray-800 rounded-2xl w-full max-w-3xl max-h-[90vh] overflow-y-auto border border-white/10 shadow-2xl">
        {/* Header */}
        <div className="sticky top-0 bg-gray-800 border-b border-white/10 p-6 flex items-center justify-between">
          <div>
            <h2 className="text-2xl font-bold text-white">Add API Source</h2>
            <p className="text-sm text-gray-400 mt-1">
              Configure external API for data ingestion
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-white/10 rounded-lg transition-all"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-6 space-y-6">
          {/* Error Message */}
          {error && (
            <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 flex items-start gap-3">
              <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="text-red-400 font-medium">Error</p>
                <p className="text-red-300 text-sm">{error}</p>
              </div>
            </div>
          )}

          {/* Basic Info */}
          <div className="space-y-4">
            <h3 className="text-lg font-semibold text-white border-b border-white/10 pb-2">
              Basic Information
            </h3>
            
            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Name <span className="text-red-400">*</span>
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) =>
                  setFormData({ ...formData, name: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                placeholder="JSONPlaceholder Posts"
                required
              />
            </div>

            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Description
              </label>
              <textarea
                value={formData.description}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                rows={2}
                placeholder="Optional description of this API source"
              />
            </div>
          </div>

          {/* API Configuration */}
          <div className="space-y-4">
            <h3 className="text-lg font-semibold text-white border-b border-white/10 pb-2">
              API Configuration
            </h3>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium mb-2 text-gray-300">
                  API Type
                </label>
                <select
                  value={formData.apiType}
                  onChange={(e) =>
                    setFormData({ ...formData, apiType: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                >
                  <option value="REST">REST</option>
                  <option value="GraphQL" disabled>GraphQL (Phase 3)</option>
                  <option value="SOAP" disabled>SOAP (Phase 3)</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium mb-2 text-gray-300">
                  HTTP Method
                </label>
                <select
                  value={formData.httpMethod}
                  onChange={(e) =>
                    setFormData({ ...formData, httpMethod: e.target.value })
                  }
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                >
                  <option value="GET">GET</option>
                  <option value="POST">POST</option>
                  <option value="PUT">PUT</option>
                  <option value="DELETE">DELETE</option>
                  <option value="PATCH">PATCH</option>
                </select>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Endpoint URL <span className="text-red-400">*</span>
              </label>
              <input
                type="url"
                value={formData.endpointUrl}
                onChange={(e) =>
                  setFormData({ ...formData, endpointUrl: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent font-mono text-sm"
                placeholder="https://api.example.com/v1/data"
                required
              />
            </div>
          </div>

          {/* Authentication */}
          <div className="space-y-4">
            <div className="flex items-center gap-2 border-b border-white/10 pb-2">
              <Lock className="w-5 h-5 text-amber-400" />
              <h3 className="text-lg font-semibold text-white">
                Authentication
              </h3>
            </div>

            <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-4 flex items-start gap-3">
              <Lock className="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" />
              <div className="flex-1 text-sm">
                <p className="text-amber-300 font-medium">Credentials are encrypted</p>
                <p className="text-amber-200/80">
                  Secrets are encrypted at rest with AES-256-GCM and never returned by the API. Only masked status is shown.
                </p>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Authentication Type
              </label>
              <select
                value={formData.authType}
                onChange={(e) => {
                  setFormData({ ...formData, authType: e.target.value });
                  setAuthConfig({});
                }}
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="None">None</option>
                <option value="Basic">Basic Auth</option>
                <option value="Bearer">Bearer Token</option>
                <option value="ApiKey">API Key</option>
                <option value="OAuth2">OAuth 2.0</option>
              </select>
            </div>

            {renderAuthFields()}
          </div>

          {/* Data Transformation */}
          <div className="space-y-4">
            <h3 className="text-lg font-semibold text-white border-b border-white/10 pb-2">
              Data Transformation
            </h3>

            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Response Path (JSONPath)
              </label>
              <input
                type="text"
                value={formData.responsePath}
                onChange={(e) =>
                  setFormData({ ...formData, responsePath: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent font-mono text-sm"
                placeholder="$ or $.data or $.items[*]"
              />
              <p className="text-xs text-gray-500 mt-1">
                Path to extract records from response (default: $)
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium mb-2 text-gray-300">
                Field Mappings (JSON)
              </label>
              <textarea
                value={formData.fieldMappings}
                onChange={(e) =>
                  setFormData({ ...formData, fieldMappings: e.target.value })
                }
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent font-mono text-sm"
                rows={3}
                placeholder='{"title": "headline", "content": "body"}'
              />
              <p className="text-xs text-gray-500 mt-1">
                Optional: Map API fields to document properties
              </p>
            </div>
          </div>

          {/* Enable */}
          <div className="flex items-center justify-between p-4 bg-white/5 rounded-lg border border-white/10">
            <div>
              <p className="font-medium text-white">Enable Source</p>
              <p className="text-sm text-gray-400">
                Start with enabled or test first before enabling
              </p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={formData.enabled}
                onChange={(e) =>
                  setFormData({ ...formData, enabled: e.target.checked })
                }
                className="sr-only peer"
              />
              <div className="w-11 h-6 bg-gray-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-green-500"></div>
            </label>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-4 border-t border-white/10">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2 bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-all"
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 px-4 py-2 bg-gradient-to-r from-blue-500 to-purple-500 hover:from-blue-600 hover:to-purple-600 rounded-lg transition-all font-medium disabled:opacity-50"
              disabled={loading}
            >
              {loading ? "Creating..." : "Create Source"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}


