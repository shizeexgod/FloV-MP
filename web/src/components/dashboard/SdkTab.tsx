'use client';

import React from 'react';
import { Download } from 'lucide-react';
import { Badge } from '@/components/ui';
import { useDashboard } from './_ctx';

export function SdkTab() {
  const {
    D,
  } = useDashboard();
  return (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <div className="flex items-center gap-2">
                <Download className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">{D.sdk.title}</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                {D.sdk.sub}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
            {[
              { badge: 'Linux x64 · 85 MB', badgeTone: 'cyber', link: '/cdn/FloVMP-Server-x64-Linux.tar.gz' },
              { badge: 'Win64 · 92 MB', badgeTone: 'brand', link: '/cdn/FloVMP-Server-x64-Windows.zip' },
              { badge: 'NuGet / DLL · 14 MB', badgeTone: 'emeraldx', link: '/cdn/FloVMP-SDK-v1.0.4.zip' },
              { badge: 'CLI Tool · 8 MB', badgeTone: 'violetx', link: '/cdn/flovmp-packer.exe' },
              { badge: 'Source · 24 MB', badgeTone: 'cyber', link: '/cdn/FloVMP-Launcher-Template.zip' },
              { badge: 'Samples · 5 MB', badgeTone: 'brand', link: '/cdn/FloVMP-Gamemode-Samples.zip' },
            ].map((item, idx) => (
              <div
                key={item.link}
                className="glass-panel card-edge flex flex-col justify-between rounded-3xl p-6 shadow-glass"
              >
                <div>
                  <div className="flex items-center justify-between">
                    <Badge tone={item.badgeTone as any}>{item.badge}</Badge>
                    <Download className="h-4 w-4 text-slate-500" />
                  </div>
                  <h3 className="mt-4 text-base font-bold text-white">{D.sdk.items[idx][0]}</h3>
                  <p className="mt-2 text-xs leading-relaxed text-slate-400">{D.sdk.items[idx][1]}</p>
                </div>
                <div className="mt-6 border-t border-white/[0.08] pt-4">
                  <a
                    href={item.link}
                    download
                    className="btn btn-ghost h-10 w-full text-xs font-semibold text-brand hover:bg-brand/10"
                  >
                    <Download className="h-4 w-4" />
                    {D.sdk.items[idx][2]}
                  </a>
                </div>
              </div>
            ))}
          </div>
        </div>
  );
}
