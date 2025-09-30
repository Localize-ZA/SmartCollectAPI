"use client";

import { useTheme } from "@/components/ThemeProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Monitor, Moon, Sun } from "lucide-react";
import { useEffect, useState } from "react";

export function ThemeStatus() {
  const { theme, actualTheme } = useTheme();
  const [systemTheme, setSystemTheme] = useState<'dark' | 'light'>('light');

  useEffect(() => {
    const getSystemTheme = () => {
      if (typeof window !== 'undefined') {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
      }
      return 'light';
    };

    setSystemTheme(getSystemTheme());

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = (e: MediaQueryListEvent) => {
      setSystemTheme(e.matches ? 'dark' : 'light');
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  const getThemeIcon = (themeType: 'dark' | 'light' | 'system') => {
    switch (themeType) {
      case 'dark': return <Moon className="h-4 w-4" />;
      case 'light': return <Sun className="h-4 w-4" />;
      case 'system': return <Monitor className="h-4 w-4" />;
    }
  };

  const getThemeVariant = (themeType: 'dark' | 'light' | 'system') => {
    switch (themeType) {
      case 'dark': return 'secondary' as const;
      case 'light': return 'outline' as const;
      case 'system': return 'default' as const;
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Theme Settings</CardTitle>
        <CardDescription>Current theme configuration and system preference</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">Theme Mode</span>
          <Badge variant={getThemeVariant(theme)} className="gap-1">
            {getThemeIcon(theme)}
            {theme.charAt(0).toUpperCase() + theme.slice(1)}
          </Badge>
        </div>
        
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">Active Theme</span>
          <Badge variant={getThemeVariant(actualTheme)} className="gap-1">
            {getThemeIcon(actualTheme)}
            {actualTheme.charAt(0).toUpperCase() + actualTheme.slice(1)}
          </Badge>
        </div>
        
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">System Preference</span>
          <Badge variant={getThemeVariant(systemTheme)} className="gap-1">
            {getThemeIcon(systemTheme)}
            {systemTheme.charAt(0).toUpperCase() + systemTheme.slice(1)}
          </Badge>
        </div>
        
        {theme === 'system' && (
          <div className="text-xs text-muted-foreground pt-2 border-t">
            Following system preference automatically
          </div>
        )}
      </CardContent>
    </Card>
  );
}