"use client";
import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { 
  Home, 
  Upload, 
  Database, 
  Activity, 
  Server, 
  Mail, 
  Settings, 
  ChevronLeft, 
  ChevronRight,
  BarChart3,
  FileText,
  Clock,
  AlertTriangle
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { SettingsModal } from "@/components/SettingsModal";
import { useSettings } from "@/components/SettingsProvider";

interface SidebarItem {
  title: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
}

const sidebarItems: SidebarItem[] = [
  {
    title: "Dashboard",
    href: "/",
    icon: Home,
  },
  {
    title: "Upload",
    href: "/upload",
    icon: Upload,
  },
  {
    title: "Documents",
    href: "/documents",
    icon: FileText,
  },
  {
    title: "Staging Queue",
    href: "/staging",
    icon: Clock,
  },
  {
    title: "Analytics",
    href: "/analytics",
    icon: BarChart3,
  },
  {
    title: "System Health",
    href: "/health",
    icon: Activity,
  },
  {
    title: "Microservices",
    href: "/microservices",
    icon: Server,
  },
  {
    title: "Email Service",
    href: "/email",
    icon: Mail,
  },
  {
    title: "Alerts",
    href: "/alerts",
    icon: AlertTriangle,
  },
];

interface SidebarProps {
  className?: string;
  collapsed?: boolean;
  onCollapsedChange?: (collapsed: boolean) => void;
}

export function Sidebar({ className, collapsed: externalCollapsed, onCollapsedChange }: SidebarProps) {
  const [internalCollapsed, setInternalCollapsed] = useState(false);
  const { isOpen: settingsOpen, openSettings, closeSettings } = useSettings();
  const pathname = usePathname();
  
  // Use external state if provided, otherwise use internal state
  const collapsed = externalCollapsed !== undefined ? externalCollapsed : internalCollapsed;
  const setCollapsed = onCollapsedChange || setInternalCollapsed;

  return (
    <div
      className={cn(
        "fixed left-0 top-0 z-50 h-full glass-effect border-r border-sidebar-border transition-all duration-300 ease-in-out shadow-lg",
        collapsed ? "w-16" : "w-64",
        className
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-sidebar-border/50">
        {!collapsed && (
          <div className="flex items-center space-x-3 animate-fade-in">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 shadow-lg ring-2 ring-primary/20">
              <Database className="h-5 w-5 text-primary-foreground" />
            </div>
            <div>
              <h2 className="text-base font-bold bg-gradient-to-r from-foreground to-foreground/70 bg-clip-text text-transparent">
                SmartCollect
              </h2>
              <p className="text-[10px] text-muted-foreground">Document AI</p>
            </div>
          </div>
        )}
        {collapsed && (
          <div className="flex h-9 w-9 items-center justify-center mx-auto rounded-xl bg-gradient-to-br from-primary to-primary/80 shadow-lg ring-2 ring-primary/20">
            <Database className="h-5 w-5 text-primary-foreground" />
          </div>
        )}
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setCollapsed(!collapsed)}
          className={cn(
            "p-2 hover:bg-sidebar-accent transition-colors",
            collapsed && "absolute right-2"
          )}
        >
          {collapsed ? (
            <ChevronRight className="h-4 w-4" />
          ) : (
            <ChevronLeft className="h-4 w-4" />
          )}
        </Button>
      </div>

      {/* Navigation with enhanced scrolling */}
      <div className="relative h-[calc(100vh-140px)]">
        {/* Top fade gradient */}
        <div className="absolute top-0 left-0 right-0 h-8 bg-gradient-to-b from-sidebar to-transparent z-10 pointer-events-none opacity-0 transition-opacity duration-300" id="scroll-fade-top" />
        
        <nav className="p-3 space-y-2 h-full overflow-y-auto scrollbar-custom scroll-smooth">
          {/* Main Section */}
          {!collapsed && (
            <div className="px-3 py-2">
              <p className="text-xs font-semibold text-muted-foreground/70 uppercase tracking-wider">
                Main
              </p>
            </div>
          )}
        
        {sidebarItems.slice(0, 4).map((item) => {
          const Icon = item.icon;
          const isActive = pathname === item.href;
          
          return (
            <Link key={item.href} href={item.href}>
              <div
                className={cn(
                  "group flex items-center space-x-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200",
                  isActive 
                    ? "bg-gradient-to-r from-primary to-primary/90 text-primary-foreground shadow-lg shadow-primary/25 scale-[1.02]" 
                    : "text-sidebar-foreground/70 hover:text-sidebar-foreground hover:bg-sidebar-accent hover:scale-[1.01]"
                )}
              >
                <Icon className={cn(
                  "h-4 w-4 flex-shrink-0 transition-transform",
                  isActive && "drop-shadow-sm",
                  !isActive && "group-hover:scale-110"
                )} />
                {!collapsed && (
                  <>
                    <span className="flex-1">{item.title}</span>
                    {item.badge && (
                      <span className="bg-primary text-primary-foreground text-xs px-2 py-0.5 rounded-full font-semibold shadow-sm">
                        {item.badge}
                      </span>
                    )}
                  </>
                )}
              </div>
            </Link>
          );
        })}

        {/* Analytics Section */}
        {!collapsed && (
          <div className="px-3 py-2 mt-6">
            <p className="text-xs font-semibold text-muted-foreground/70 uppercase tracking-wider">
              Analytics & Monitoring
            </p>
          </div>
        )}
        
        {sidebarItems.slice(4, 7).map((item) => {
          const Icon = item.icon;
          const isActive = pathname === item.href;
          
          return (
            <Link key={item.href} href={item.href}>
              <div
                className={cn(
                  "group flex items-center space-x-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200",
                  isActive 
                    ? "bg-gradient-to-r from-primary to-primary/90 text-primary-foreground shadow-lg shadow-primary/25 scale-[1.02]" 
                    : "text-sidebar-foreground/70 hover:text-sidebar-foreground hover:bg-sidebar-accent hover:scale-[1.01]"
                )}
              >
                <Icon className={cn(
                  "h-4 w-4 flex-shrink-0 transition-transform",
                  isActive && "drop-shadow-sm",
                  !isActive && "group-hover:scale-110"
                )} />
                {!collapsed && <span className="flex-1">{item.title}</span>}
              </div>
            </Link>
          );
        })}

        {/* Services Section */}
        {!collapsed && (
          <div className="px-3 py-2 mt-6">
            <p className="text-xs font-semibold text-muted-foreground/70 uppercase tracking-wider">
              Services
            </p>
          </div>
        )}
        
        {sidebarItems.slice(7).map((item) => {
          const Icon = item.icon;
          const isActive = pathname === item.href;
          
          return (
            <Link key={item.href} href={item.href}>
              <div
                className={cn(
                  "group flex items-center space-x-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200",
                  isActive 
                    ? "bg-gradient-to-r from-primary to-primary/90 text-primary-foreground shadow-lg shadow-primary/25 scale-[1.02]" 
                    : "text-sidebar-foreground/70 hover:text-sidebar-foreground hover:bg-sidebar-accent hover:scale-[1.01]"
                )}
              >
                <Icon className={cn(
                  "h-4 w-4 flex-shrink-0 transition-transform",
                  isActive && "drop-shadow-sm",
                  !isActive && "group-hover:scale-110"
                )} />
                {!collapsed && <span className="flex-1">{item.title}</span>}
              </div>
            </Link>
          );
        })}
        
        {/* Settings Button */}
        <div className={cn("mt-6", !collapsed && "pt-6 border-t border-sidebar-border/50")}>
          <button
            onClick={openSettings}
            className={cn(
              "w-full group flex items-center space-x-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200 text-sidebar-foreground/70 hover:text-sidebar-foreground hover:bg-sidebar-accent hover:scale-[1.01]"
            )}
          >
            <Settings className="h-4 w-4 flex-shrink-0 transition-transform group-hover:rotate-90 group-hover:scale-110" />
            {!collapsed && <span className="flex-1 text-left">Settings</span>}
          </button>
        </div>
        </nav>
        
        {/* Bottom fade gradient */}
        <div className="absolute bottom-0 left-0 right-0 h-8 bg-gradient-to-t from-sidebar to-transparent z-10 pointer-events-none" />
      </div>

      {/* Footer */}
      <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-sidebar-border/50 bg-gradient-to-t from-sidebar/95 to-transparent">        
        {!collapsed ? (
          <div className="text-xs text-sidebar-foreground/50 text-center space-y-1 animate-fade-in">
            <p className="font-semibold">SmartCollect API</p>
            <p className="text-[10px]">Document Intelligence Platform</p>
            <p className="text-[10px] font-mono">v1.0.0</p>
          </div>
        ) : (
          <div className="flex justify-center">
            <div className="h-1 w-1 rounded-full bg-primary animate-pulse" />
          </div>
        )}
      </div>

      {/* Settings Modal */}
      <SettingsModal 
        isOpen={settingsOpen} 
        onClose={closeSettings} 
      />
    </div>
  );
}