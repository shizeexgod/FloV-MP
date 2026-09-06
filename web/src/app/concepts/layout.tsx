import React from 'react';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'FloV:MP — Дизайн-концепты платформы',
  description: 'Три визуальных направления публичного сайта и дашборда FloV:MP. Прототипы для выбора направления.',
};

/* Self-hosted Inter + JetBrains Mono (OFL). Files in /public/fonts — no network at build. */
const FONT_CSS = `
@font-face{font-family:'InterC';font-style:normal;font-weight:100 900;font-display:swap;src:url('/fonts/inter-latin.woff2') format('woff2');unicode-range:U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+2074,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD}
@font-face{font-family:'InterC';font-style:normal;font-weight:100 900;font-display:swap;src:url('/fonts/inter-latin-ext.woff2') format('woff2');unicode-range:U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF}
@font-face{font-family:'InterC';font-style:normal;font-weight:100 900;font-display:swap;src:url('/fonts/inter-cyrillic.woff2') format('woff2');unicode-range:U+0301,U+0400-045F,U+0490-0491,U+04B0-04B1,U+2116}
@font-face{font-family:'JetBrainsMonoC';font-style:normal;font-weight:100 800;font-display:swap;src:url('/fonts/jbmono-latin.woff2') format('woff2');unicode-range:U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+2074,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD}
@font-face{font-family:'JetBrainsMonoC';font-style:normal;font-weight:100 800;font-display:swap;src:url('/fonts/jbmono-latin-ext.woff2') format('woff2');unicode-range:U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF}
@font-face{font-family:'JetBrainsMonoC';font-style:normal;font-weight:100 800;font-display:swap;src:url('/fonts/jbmono-cyrillic.woff2') format('woff2');unicode-range:U+0301,U+0400-045F,U+0490-0491,U+04B0-04B1,U+2116}

body:has(.concepts-root) > header,
body:has(.concepts-root) > footer { display: none !important; }
body:has(.concepts-root) { background: #08080a; }
body:has(.concepts-root) > main { flex: 1 1 auto; }

.concepts-root{
  --font-c-sans:'InterC';
  --font-c-mono:'JetBrainsMonoC';
  font-family:'InterC',ui-sans-serif,system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;
  font-feature-settings:'cv11','ss01';
  -webkit-font-smoothing:antialiased;
  text-rendering:optimizeLegibility;
}
.concepts-root .font-mono,
.concepts-root code,
.concepts-root pre,
.concepts-root kbd{ font-family:'JetBrainsMonoC',ui-monospace,'SFMono-Regular',Menlo,monospace; }
.concepts-root ::selection{ background:rgba(255,20,147,.28); color:#fff; }
`;

export default function ConceptsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="concepts-root">
      <style dangerouslySetInnerHTML={{ __html: FONT_CSS }} />
      {children}
    </div>
  );
}
