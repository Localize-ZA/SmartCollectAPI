"use client";

import { useEffect } from 'react';

// This component handles theme initialization before hydration
export function ThemeScript() {
  useEffect(() => {
    // This will run on the client after hydration
    const initializeTheme = () => {
      try {
        const savedTheme = localStorage.getItem('smartcollect-theme') || 'system';
        let themeToApply: 'dark' | 'light';

        if (savedTheme === 'system') {
          themeToApply = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        } else {
          themeToApply = savedTheme as 'dark' | 'light';
        }

        document.documentElement.classList.remove('dark', 'light');
        document.documentElement.classList.add(themeToApply);
      } catch (error) {
        // Fallback to light theme
        document.documentElement.classList.remove('dark');
        document.documentElement.classList.add('light');
      }
    };

    initializeTheme();
  }, []);

  return null;
}

// Inline script to prevent flash of unstyled content
export const themeScript = `
  (function() {
    try {
      const savedTheme = localStorage.getItem('smartcollect-theme') || 'system';
      let themeToApply;
      
      if (savedTheme === 'system') {
        themeToApply = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
      } else {
        themeToApply = savedTheme;
      }
      
      document.documentElement.classList.remove('dark', 'light');
      document.documentElement.classList.add(themeToApply);
    } catch (e) {
      document.documentElement.classList.add('light');
    }
  })();
`;