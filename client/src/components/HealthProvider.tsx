'use client'

import { createContext, useContext, useEffect, useState, useTransition } from 'react'
import { getServerHealthStatus, refreshHealthStatus } from '@/lib/server-actions'

interface HealthStatus {
  status: string
  ts: string
  lastChecked: number
}

interface HealthContextType {
  healthStatus: HealthStatus
  isRefreshing: boolean
  error: string | null
  refresh: () => void
}

const HealthContext = createContext<HealthContextType | undefined>(undefined)

export function HealthProvider({ children }: { children: React.ReactNode }) {
  const [healthStatus, setHealthStatus] = useState<HealthStatus>({
    status: 'unknown',
    ts: '',
    lastChecked: 0
  })
  const [isRefreshing, startTransition] = useTransition()
  const [error, setError] = useState<string | null>(null)

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

  const refresh = () => {
    startTransition(() => {
      loadHealthStatus(true)
    })
  }

  // Initial load and background refresh
  useEffect(() => {
    loadHealthStatus(false)

    // Set up background refresh every 15 seconds
    const interval = setInterval(() => {
      startTransition(() => {
        loadHealthStatus(false)
      })
    }, 15000)

    return () => clearInterval(interval)
  }, [])

  const value = {
    healthStatus,
    isRefreshing,
    error,
    refresh
  }

  return (
    <HealthContext.Provider value={value}>
      {children}
    </HealthContext.Provider>
  )
}

export function useGlobalHealthStatus() {
  const context = useContext(HealthContext)
  if (context === undefined) {
    throw new Error('useGlobalHealthStatus must be used within a HealthProvider')
  }
  return context
}