"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useSettings } from "@/components/SettingsProvider";


export default function SettingsPage() {
  const router = useRouter();
  const { openSettings } = useSettings();

  useEffect(() => {
    // Open the settings modal and redirect to home
    openSettings();
    router.replace('/');
  }, [openSettings, router]);

  return (
    <div className="flex items-center justify-center h-96">
      <div className="text-center">
        <p className="text-muted-foreground">Opening settings...</p>
      </div>
    </div>
  );
}