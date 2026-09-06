import React from 'react';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'FloV:MP — Дизайн-концепты платформы',
  description: 'Три визуальных направления публичного сайта и дашборда FloV:MP. Прототипы для выбора направления.',
};

export default function ConceptsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="concepts-root">
      {/* Hide the production site chrome while exploring concepts */}
      <style
        dangerouslySetInnerHTML={{
          __html: `
            body:has(.concepts-root) > header,
            body:has(.concepts-root) > footer { display: none !important; }
            body:has(.concepts-root) { background: #08080a; }
            body:has(.concepts-root) > main { flex: 1 1 auto; }
          `,
        }}
      />
      {children}
    </div>
  );
}
