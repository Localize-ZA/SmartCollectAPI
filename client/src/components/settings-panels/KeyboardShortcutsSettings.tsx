import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";

export function KeyboardShortcutsSettings() {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Available Shortcuts</CardTitle>
          <CardDescription>
            Keyboard shortcuts for faster navigation and actions
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="grid gap-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Toggle theme</span>
                <Badge variant="outline" className="font-mono">Ctrl + Shift + T</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Toggle sidebar</span>
                <Badge variant="outline" className="font-mono">Ctrl + B</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Open settings</span>
                <Badge variant="outline" className="font-mono">Ctrl + ,</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Refresh dashboard</span>
                <Badge variant="outline" className="font-mono">F5</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Search documents</span>
                <Badge variant="outline" className="font-mono">Ctrl + K</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Upload documents</span>
                <Badge variant="outline" className="font-mono">Ctrl + U</Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Navigation Shortcuts</CardTitle>
          <CardDescription>
            Quick navigation between different sections
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Go to dashboard</span>
              <Badge variant="outline" className="font-mono">Alt + 1</Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Go to documents</span>
              <Badge variant="outline" className="font-mono">Alt + 2</Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Go to staging</span>
              <Badge variant="outline" className="font-mono">Alt + 3</Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Go to health status</span>
              <Badge variant="outline" className="font-mono">Alt + 4</Badge>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}