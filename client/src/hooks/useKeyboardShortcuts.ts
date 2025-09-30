"use client";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTheme } from "@/components/ThemeProvider";
import { useSettings } from "@/components/SettingsProvider";

export function useKeyboardShortcuts() {
  const { theme, setTheme, actualTheme } = useTheme();
  const { openSettings } = useSettings();
  const router = useRouter();

  const toggleTheme = () => {
    if (theme === 'system') {
      setTheme(actualTheme === 'dark' ? 'light' : 'dark');
    } else if (theme === 'dark') {
      setTheme('light');
    } else {
      setTheme('dark');
    }
  };

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      // Toggle theme: Ctrl + Shift + T
      if (event.ctrlKey && event.shiftKey && event.key === 'T') {
        event.preventDefault();
        toggleTheme();
        return;
      }

      // Open settings: Ctrl + ,
      if (event.ctrlKey && !event.shiftKey && event.key === ',') {
        event.preventDefault();
        openSettings();
        return;
      }

      // Navigate to dashboard: Ctrl + Shift + H
      if (event.ctrlKey && event.shiftKey && event.key === 'H') {
        event.preventDefault();
        router.push('/');
        return;
      }

      // Navigate to upload: Ctrl + Shift + U
      if (event.ctrlKey && event.shiftKey && event.key === 'U') {
        event.preventDefault();
        router.push('/upload');
        return;
      }

      // Navigate to documents: Ctrl + Shift + D
      if (event.ctrlKey && event.shiftKey && event.key === 'D') {
        event.preventDefault();
        router.push('/documents');
        return;
      }
    };

    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [theme, setTheme, actualTheme, openSettings, router]);
}