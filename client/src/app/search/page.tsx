"use client";

import { useState } from "react";
import { DashboardLayout } from "@/components/DashboardLayout";
import { Search, Sparkles, FileText, ChevronDown, ChevronUp, Clock, Hash } from "lucide-react";
import { searchChunks, hybridSearchChunks, type ChunkSearchResult } from "@/lib/api";

export default function SearchPage() {
  const [query, setQuery] = useState("");
  const [provider, setProvider] = useState("sentence-transformers");
  const [searchType, setSearchType] = useState<"semantic" | "hybrid">("semantic");
  const [limit, setLimit] = useState(10);
  const [similarityThreshold, setSimilarityThreshold] = useState(0.7);
  const [semanticWeight, setSemanticWeight] = useState(0.7);
  const [results, setResults] = useState<ChunkSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [searchTime, setSearchTime] = useState<number | null>(null);
  const [expandedChunks, setExpandedChunks] = useState<Set<number>>(new Set());

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;

    setLoading(true);
    setError("");
    setResults([]);
    const startTime = Date.now();

    try {
      let response;
      if (searchType === "semantic") {
        response = await searchChunks({
          query,
          provider,
          limit,
          similarityThreshold
        });
      } else {
        response = await hybridSearchChunks({
          query,
          provider,
          limit,
          similarityThreshold,
          semanticWeight,
          textWeight: 1 - semanticWeight
        });
      }

      setResults(response.results);
      setSearchTime(Date.now() - startTime);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Search failed");
    } finally {
      setLoading(false);
    }
  };

  const toggleChunk = (chunkId: number) => {
    setExpandedChunks(prev => {
      const next = new Set(prev);
      if (next.has(chunkId)) {
        next.delete(chunkId);
      } else {
        next.add(chunkId);
      }
      return next;
    });
  };

  const getHighlightedContent = (content: string, query: string) => {
    if (!query.trim()) return content;
    
    const terms = query.toLowerCase().split(/\s+/);
    let highlighted = content;
    
    terms.forEach(term => {
      if (term.length < 3) return;
      const regex = new RegExp(`(${term})`, 'gi');
      highlighted = highlighted.replace(regex, '<mark class="bg-yellow-300 dark:bg-yellow-600 px-1 rounded">$1</mark>');
    });
    
    return highlighted;
  };

  return (
    <DashboardLayout>
      <div className="space-y-6 animate-fade-in">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-primary/20 to-primary/5 ring-1 ring-primary/10">
            <Search className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">
              Semantic Chunk Search
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Search through document chunks with AI-powered semantic understanding
            </p>
          </div>
        </div>

        {/* Search Form */}
        <div className="glass-effect rounded-xl p-6 ring-1 ring-border/50">
          <form onSubmit={handleSearch} className="space-y-4">
            <div>
              <label className="block text-sm font-medium mb-2">
                Search Query
              </label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-muted-foreground" />
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="e.g., 'machine learning algorithms' or 'customer feedback analysis'"
                  className="w-full pl-10 pr-4 py-3 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary focus:border-transparent"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <div>
                <label className="block text-sm font-medium mb-2">
                  Search Type
                </label>
                <select
                  value={searchType}
                  onChange={(e) => setSearchType(e.target.value as "semantic" | "hybrid")}
                  className="w-full px-3 py-2 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary"
                >
                  <option value="semantic">Semantic (Embedding-based)</option>
                  <option value="hybrid">Hybrid (Semantic + Text)</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium mb-2">
                  Provider
                </label>
                <select
                  value={provider}
                  onChange={(e) => setProvider(e.target.value)}
                  className="w-full px-3 py-2 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary"
                >
                  <option value="sentence-transformers">Sentence Transformers (768d)</option>
                  <option value="spacy">spaCy (300d)</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium mb-2">
                  Results Limit
                </label>
                <input
                  type="number"
                  value={limit}
                  onChange={(e) => setLimit(parseInt(e.target.value) || 10)}
                  min="1"
                  max="50"
                  className="w-full px-3 py-2 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary"
                />
              </div>

              <div>
                <label className="block text-sm font-medium mb-2">
                  Similarity Threshold
                </label>
                <input
                  type="number"
                  value={similarityThreshold}
                  onChange={(e) => setSimilarityThreshold(parseFloat(e.target.value) || 0.7)}
                  min="0"
                  max="1"
                  step="0.05"
                  className="w-full px-3 py-2 bg-background border border-border rounded-lg focus:ring-2 focus:ring-primary"
                />
              </div>
            </div>

            {searchType === "hybrid" && (
              <div>
                <label className="block text-sm font-medium mb-2">
                  Semantic Weight: {semanticWeight.toFixed(2)} (Text: {(1 - semanticWeight).toFixed(2)})
                </label>
                <input
                  type="range"
                  value={semanticWeight}
                  onChange={(e) => setSemanticWeight(parseFloat(e.target.value))}
                  min="0"
                  max="1"
                  step="0.1"
                  className="w-full"
                />
              </div>
            )}

            <button
              type="submit"
              disabled={loading || !query.trim()}
              className="w-full px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 font-medium"
            >
              {loading ? (
                <>
                  <div className="h-5 w-5 border-2 border-current border-t-transparent rounded-full animate-spin" />
                  Searching...
                </>
              ) : (
                <>
                  <Sparkles className="h-5 w-5" />
                  Search
                </>
              )}
            </button>
          </form>
        </div>

        {/* Results */}
        {error && (
          <div className="glass-effect rounded-xl p-4 ring-1 ring-destructive/50 bg-destructive/10">
            <p className="text-destructive font-medium">{error}</p>
          </div>
        )}

        {searchTime !== null && (
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <div className="flex items-center gap-2">
              <Clock className="h-4 w-4" />
              <span>Search completed in {searchTime}ms</span>
            </div>
            <div className="flex items-center gap-2">
              <Hash className="h-4 w-4" />
              <span>{results.length} chunks found</span>
            </div>
          </div>
        )}

        {results.length > 0 && (
          <div className="space-y-4">
            {results.map((result, index) => (
              <div
                key={`${result.chunkId}-${index}`}
                className="glass-effect rounded-xl p-6 ring-1 ring-border/50 hover-lift"
              >
                <div className="flex items-start justify-between gap-4 mb-4">
                  <div className="flex items-start gap-3 flex-1">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 flex-shrink-0">
                      <FileText className="h-5 w-5 text-primary" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <h3 className="font-medium text-sm truncate">
                        {result.documentUri}
                      </h3>
                      <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
                        <span>Chunk {result.chunkIndex + 1}</span>
                        <span>•</span>
                        <span>Similarity: {(result.similarity * 100).toFixed(1)}%</span>
                      </div>
                    </div>
                  </div>
                  
                  <button
                    onClick={() => toggleChunk(result.chunkId)}
                    className="px-3 py-1.5 text-sm rounded-lg border border-border hover:bg-accent flex items-center gap-1"
                  >
                    {expandedChunks.has(result.chunkId) ? (
                      <>
                        <ChevronUp className="h-4 w-4" />
                        Collapse
                      </>
                    ) : (
                      <>
                        <ChevronDown className="h-4 w-4" />
                        Expand
                      </>
                    )}
                  </button>
                </div>

                <div className={`prose prose-sm dark:prose-invert max-w-none ${
                  expandedChunks.has(result.chunkId) ? '' : 'line-clamp-3'
                }`}>
                  <p
                    dangerouslySetInnerHTML={{
                      __html: getHighlightedContent(result.content, query)
                    }}
                  />
                </div>

                <div className="mt-4 pt-4 border-t border-border flex items-center justify-between text-xs text-muted-foreground">
                  <span>Document ID: {result.documentId.substring(0, 8)}...</span>
                  <a
                    href={`/documents/${result.documentId}`}
                    className="text-primary hover:underline"
                  >
                    View Document →
                  </a>
                </div>
              </div>
            ))}
          </div>
        )}

        {!loading && results.length === 0 && searchTime === null && (
          <div className="text-center py-12 glass-effect rounded-xl ring-1 ring-border/50">
            <Sparkles className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-lg font-medium mb-2">Ready to Search</p>
            <p className="text-muted-foreground">
              Enter a query to search through document chunks with semantic understanding
            </p>
          </div>
        )}

        {!loading && results.length === 0 && searchTime !== null && (
          <div className="text-center py-12 glass-effect rounded-xl ring-1 ring-border/50">
            <Search className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-lg font-medium mb-2">No Results Found</p>
            <p className="text-muted-foreground">
              Try adjusting your query or lowering the similarity threshold
            </p>
          </div>
        )}
      </div>
    </DashboardLayout>
  );
}
