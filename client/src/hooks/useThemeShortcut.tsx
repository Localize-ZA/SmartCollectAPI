"use client";

import { useKeyboardShortcuts } from "@/hooks/useKeyboardShortcuts";

interface ThemeShortcutProviderProps {
  children: React.ReactNode;
}

export function ThemeShortcutProvider({ children }: ThemeShortcutProviderProps) {
  useKeyboardShortcuts();
  
  return <>{children}</>;
}

// Legacy hook for backward compatibility
export function useThemeShortcut() {
  useKeyboardShortcuts();
}