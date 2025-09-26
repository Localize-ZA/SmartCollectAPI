'use server'

import { unstable_cache } from 'next/cache'

interface HealthStatus {
  status: string
  ts: string
  lastChecked: number
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5082"

// Server-side health check function with optimized error handling
async function fetchHealthStatus(): Promise<HealthStatus> {
  const startTime = Date.now()
  
  try {
    // Use AbortController for better timeout control
    const controller = new AbortController()
    const timeoutId = setTimeout(() => controller.abort(), 5000)
    
    const response = await fetch(`${API_BASE}/health/basic`, {
      cache: 'no-store',
      signal: controller.signal,
      headers: {
        'User-Agent': 'SmartCollect-Dashboard/1.0',
        'Accept': 'application/json',
      }
    })
    
    clearTimeout(timeoutId)
    
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`)
    }
    
    const data = await response.json()
    
    // Validate response structure
    if (!data || typeof data.status !== 'string') {
      throw new Error('Invalid response format from health endpoint')
    }
    
    return {
      status: data.status,
      ts: data.ts || new Date().toISOString(),
      lastChecked: Date.now()
    }
  } catch (error) {
    const responseTime = Date.now() - startTime
    
    // Log detailed error information for debugging
    console.error(`Health check failed after ${responseTime}ms:`, {
      error: error instanceof Error ? error.message : String(error),
      endpoint: `${API_BASE}/health/basic`,
      timestamp: new Date().toISOString()
    })
    
    return {
      status: 'unreachable',
      ts: new Date().toISOString(),
      lastChecked: Date.now()
    }
  }
}

// Cached version with optimized cache settings
const getCachedHealthStatus = unstable_cache(
  fetchHealthStatus,
  ['health-status'],
  {
    revalidate: 8, // Cache for 8 seconds to balance freshness and performance
    tags: ['health']
  }
)

// Server action to get health status (uses cache)
export async function getServerHealthStatus(): Promise<HealthStatus> {
  return getCachedHealthStatus()
}

// Server action to force refresh health status (bypasses cache)
export async function refreshHealthStatus(): Promise<HealthStatus> {
  try {
    // Force revalidate the cache
    const { revalidateTag } = await import('next/cache')
    revalidateTag('health')
    
    // Return fresh data directly
    return await fetchHealthStatus()
  } catch (error) {
    console.error('Force refresh failed:', error)
    // Fallback to cached version if force refresh fails
    return getCachedHealthStatus()
  }
}