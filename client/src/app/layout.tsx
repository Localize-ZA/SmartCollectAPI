import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { HealthProvider } from "@/components/HealthProvider";
import { ThemeProvider } from "@/components/ThemeProvider";
import { ThemeShortcutProvider } from "@/hooks/useThemeShortcut";
import { SettingsProvider } from "@/components/SettingsProvider";
import { themeScript } from "@/components/ThemeScript";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "SmartCollect API Dashboard",
  description: "Monitor and manage your SmartCollect API pipeline",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        <ThemeProvider>
          <SettingsProvider>
            <ThemeShortcutProvider>
              <HealthProvider>
                {children}
              </HealthProvider>
            </ThemeShortcutProvider>
          </SettingsProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
