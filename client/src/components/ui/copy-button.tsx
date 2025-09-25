"use client";

import { type ComponentProps, type ReactNode, useState } from "react";
import { Button } from "@/components/ui/button";
import { Check, Copy } from "lucide-react";

type CopyButtonProps = ComponentProps<typeof Button> & {
  value: string;
  copiedLabel?: string;
  children?: ReactNode;
};

export function CopyButton({ value, copiedLabel = "Copied!", children, ...props }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      console.error("Failed to copy", error);
    }
  }

  return (
    <Button type="button" size="icon" variant="ghost" onClick={handleCopy} {...props}>
      {copied ? <Check className="size-4" aria-hidden /> : <Copy className="size-4" aria-hidden />}
      <span className="sr-only">{copied ? copiedLabel : "Copy"}</span>
      {children}
    </Button>
  );
}
