"use client";
import { useState, useEffect, createContext, useContext } from "react";
import { Sidebar } from "@/components/Sidebar";
import { cn } from "@/lib/utils";

interface SidebarContextType {
  collapsed: boolean;
  setCollapsed: (collapsed: boolean) => void;
}

const SidebarContext = createContext<SidebarContextType | undefined>(undefined);

export const useSidebar = () => {
  const context = useContext(SidebarContext);
  if (!context) {
    throw new Error('useSidebar must be used within a DashboardLayout');
  }
  return context;
};

interface DashboardLayoutProps {
  children: React.ReactNode;
}

export function DashboardLayout({ children }: DashboardLayoutProps) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  // Listen for sidebar state changes
  useEffect(() => {
    const handleResize = () => {
      if (window.innerWidth < 768) {
        setSidebarCollapsed(true);
      } else if (window.innerWidth >= 1024) {
        setSidebarCollapsed(false);
      }
    };

    window.addEventListener('resize', handleResize);
    handleResize(); // Check initial size

    return () => window.removeEventListener('resize', handleResize);
  }, []);

  return (
    <SidebarContext.Provider value={{ collapsed: sidebarCollapsed, setCollapsed: setSidebarCollapsed }}>
      <div className="min-h-screen bg-gradient-to-br from-background via-background to-muted/20">
        <Sidebar collapsed={sidebarCollapsed} onCollapsedChange={setSidebarCollapsed} />
        <main
          className={cn(
            "transition-all duration-300 ease-in-out min-h-screen",
            sidebarCollapsed ? "ml-16" : "ml-64"
          )}
        >
          {/* Header */}
          <header className="sticky top-0 z-40 border-b border-border/40 glass-effect shadow-sm">
            <div className="flex h-16 items-center px-6">
              <div className="flex items-center justify-between w-full">
                <div className="flex items-center space-x-4">
                  <div className="flex items-center gap-3">
                    <div className="h-2 w-2 rounded-full bg-success animate-pulse shadow-lg shadow-success/50" />
                    <div>
                      <h1 className="text-sm font-semibold text-foreground/90">
                        SmartCollect Dashboard
                      </h1>
                      <p className="text-xs text-muted-foreground">
                        Intelligent Document Processing
                      </p>
                    </div>
                  </div>
                </div>
                
                {/* Header Actions */}
                <div className="flex items-center gap-2">
                  <div className="hidden md:flex items-center gap-2 text-xs text-muted-foreground">
                    <span className="flex items-center gap-1.5">
                      <span className="h-1.5 w-1.5 rounded-full bg-success" />
                      All Systems Operational
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </header>
          
          {/* Content */}
          <div className="p-6 lg:p-8">
            <div className="mx-auto max-w-screen-2xl">
              {children}
            </div>
          </div>

          {/* Background Effects */}
          <div className="fixed inset-0 -z-10 overflow-hidden pointer-events-none">
            <div className="absolute top-0 -left-4 w-96 h-96 bg-primary/5 rounded-full blur-3xl" />
            <div className="absolute bottom-0 -right-4 w-96 h-96 bg-chart-3/5 rounded-full blur-3xl" />
          </div>
        </main>
      </div>
    </SidebarContext.Provider>
  );
}