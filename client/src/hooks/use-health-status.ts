'use client'

import { useState, useEffect, useTransition, useRef } from 'react'
import { getServerHealthStatus, refreshHealthStatus } from '@/lib/server-actions'

interface HealthStatus {
  status: string
  ts: string
  lastChecked: number
}

export function useHealthStatus() {
  const [healthStatus, setHealthStatus] = useState<HealthStatus>({
    status: 'unknown',
    ts: '',
    lastChecked: 0
  })
  const [isRefreshing, startTransition] = useTransition()
  const [error, setError] = useState<string | null>(null)
  const intervalRef = useRef<NodeJS.Timeout | null>(null)
  
  // Function to load health status
  const loadHealthStatus = async (force = false) => {
    try {
      setError(null)
      const status = force 
        ? await refreshHealthStatus()
        : await getServerHealthStatus()
      
      setHealthStatus(status)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error'
      setError(message)
      setHealthStatus(prev => ({
        ...prev,
        status: 'unreachable',
        lastChecked: Date.now()
      }))
    }
  }

  // Manual refresh function
  const refresh = () => {
    startTransition(() => {
      loadHealthStatus(true)
    })
  }

  // Background refresh function (uses cache)
  const backgroundRefresh = () => {
    startTransition(() => {
      loadHealthStatus(false)
    })
  }

  // Initial load
  useEffect(() => {
    loadHealthStatus(false)
  }, [])

  // Set up background refresh interval
  useEffect(() => {
    // Clear any existing interval
    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    // Set up new interval for background refresh every 15 seconds
    intervalRef.current = setInterval(() => {
      backgroundRefresh()
    }, 15000)

    // Cleanup on unmount
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [])

  // Cleanup interval on unmount
  useEffect(() => {
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [])

  return {
    healthStatus,
    isRefreshing,
    error,
    refresh,
    // Computed values for easier use
    status: healthStatus.status,
    timestamp: healthStatus.ts ? new Date(healthStatus.ts).toLocaleString() : '',
    lastChecked: healthStatus.lastChecked,
    isHealthy: healthStatus.status === 'ok',
    isUnreachable: healthStatus.status === 'unreachable'
  }
}