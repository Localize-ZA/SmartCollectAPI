"use client";

import { useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { 
  Palette, 
  Monitor, 
  Bell, 
  Shield, 
  Database, 
  Keyboard,
  X
} from "lucide-react";

// Import individual settings panels
import { AppearanceSettings } from "./settings-panels/AppearanceSettings";
import { SystemMonitoringSettings } from "./settings-panels/SystemMonitoringSettings";
import { NotificationsSettings } from "./settings-panels/NotificationsSettings";
import { SecuritySettings } from "./settings-panels/SecuritySettings";
import { DatabaseSettings } from "./settings-panels/DatabaseSettings";
import { KeyboardShortcutsSettings } from "./settings-panels/KeyboardShortcutsSettings";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

type SettingsTab = 
  | "appearance" 
  | "system" 
  | "notifications" 
  | "security" 
  | "database" 
  | "shortcuts";

const settingsTabs = [
  {
    id: "appearance" as SettingsTab,
    label: "Appearance",
    icon: Palette,
    description: "Theme and visual preferences"
  },
  {
    id: "system" as SettingsTab,
    label: "System Monitoring",
    icon: Monitor,
    description: "Performance and health monitoring"
  },
  {
    id: "notifications" as SettingsTab,
    label: "Notifications",
    icon: Bell,
    description: "Alert and notification settings"
  },
  {
    id: "security" as SettingsTab,
    label: "Security",
    icon: Shield,
    description: "Security and access control"
  },
  {
    id: "database" as SettingsTab,
    label: "Database",
    icon: Database,
    description: "Database configuration"
  },
  {
    id: "shortcuts" as SettingsTab,
    label: "Keyboard Shortcuts",
    icon: Keyboard,
    description: "Available keyboard shortcuts"
  }
];

export function SettingsModal({ isOpen, onClose }: SettingsModalProps) {
  const [activeTab, setActiveTab] = useState<SettingsTab>("appearance");

  const renderActivePanel = () => {
    switch (activeTab) {
      case "appearance":
        return <AppearanceSettings />;
      case "system":
        return <SystemMonitoringSettings />;
      case "notifications":
        return <NotificationsSettings />;
      case "security":
        return <SecuritySettings />;
      case "database":
        return <DatabaseSettings />;
      case "shortcuts":
        return <KeyboardShortcutsSettings />;
      default:
        return <AppearanceSettings />;
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-5xl w-[85vw] h-[75vh] max-h-[650px] p-0 overflow-hidden">
        <div className="flex h-full">
          {/* Sidebar */}
          <div className="w-64 border-r bg-muted/30 p-4 flex-shrink-0">
            <DialogHeader className="px-2 pb-4">
              <DialogTitle className="text-lg">Settings</DialogTitle>
            </DialogHeader>
            
            <nav className="space-y-1">
              {settingsTabs.map((tab) => {
                const Icon = tab.icon;
                return (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={cn(
                      "w-full flex items-start gap-3 px-3 py-2.5 text-left rounded-lg transition-colors",
                      "hover:bg-muted/50",
                      activeTab === tab.id 
                        ? "bg-primary/10 text-primary border border-primary/20" 
                        : "text-muted-foreground hover:text-foreground"
                    )}
                  >
                    <Icon className="h-4 w-4 mt-0.5 shrink-0" />
                    <div className="min-w-0">
                      <div className="text-sm font-medium truncate">
                        {tab.label}
                      </div>
                      <div className="text-xs text-muted-foreground truncate">
                        {tab.description}
                      </div>
                    </div>
                  </button>
                );
              })}
            </nav>
          </div>

          {/* Main Content */}
          <div className="flex-1 flex flex-col">
            {/* Header */}
            <div className="flex items-center justify-between p-4 border-b">
              <div>
                <h2 className="text-lg font-semibold">
                  {settingsTabs.find(tab => tab.id === activeTab)?.label}
                </h2>
                <p className="text-sm text-muted-foreground">
                  {settingsTabs.find(tab => tab.id === activeTab)?.description}
                </p>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={onClose}
                className="h-8 w-8"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>

            {/* Panel Content */}
            <div className="flex-1 overflow-y-auto p-4">
              {renderActivePanel()}
            </div>

            {/* Footer */}
            <div className="border-t p-4 flex justify-end space-x-3">
              <Button variant="outline" onClick={onClose}>
                Cancel
              </Button>
              <Button>
                Save Changes
              </Button>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}