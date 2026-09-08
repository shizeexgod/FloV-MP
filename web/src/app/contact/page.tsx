'use client';

import React, { useState } from 'react';
import { MessageCircle, Send } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';

export default function ContactPage() {
  const t = useT();
  const [sent, setSent] = useState(false);

  return (
    <div>
      <PageHero eyebrow={t.nav.contact} title={t.contact.title} sub={t.contact.sub} />

      <Container className="py-14 sm:py-16">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_1fr]">
          <div className="space-y-4">
            <Reveal>
              <a
                href="https://t.me/flovmp_dev"
                target="_blank"
                rel="noopener noreferrer"
                className="card card-hover flex items-start gap-4 p-6"
              >
                <span className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-brand">
                  <Send className="h-4 w-4" />
                </span>
                <div>
                  <div className="text-[14px] font-semibold text-white">{t.contact.tgTitle}</div>
                  <p className="mt-1 text-[12.5px] leading-relaxed text-white/50">{t.contact.tgDesc}</p>
                  <span className="mt-2 inline-block font-mono text-[12px] text-brand">@flovmp_dev</span>
                </div>
              </a>
            </Reveal>
            <Reveal delay={70}>
              <a
                href="https://discord.gg/flovmp"
                target="_blank"
                rel="noopener noreferrer"
                className="card card-hover flex items-start gap-4 p-6"
              >
                <span className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-brand">
                  <MessageCircle className="h-4 w-4" />
                </span>
                <div>
                  <div className="text-[14px] font-semibold text-white">{t.contact.dsTitle}</div>
                  <p className="mt-1 text-[12.5px] leading-relaxed text-white/50">{t.contact.dsDesc}</p>
                  <span className="mt-2 inline-block font-mono text-[12px] text-brand">discord.gg/flovmp</span>
                </div>
              </a>
            </Reveal>
          </div>

          <Reveal delay={120}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                setSent(true);
              }}
              className="card p-6"
            >
              <h2 className="text-[15px] font-semibold text-white">{t.contact.formTitle}</h2>
              <div className="mt-4 space-y-3">
                <input required placeholder={t.contact.formName} className="field h-10 px-3" />
                <input required placeholder={t.contact.formContact} className="field h-10 px-3" />
                <textarea required rows={4} placeholder={t.contact.formProject} className="field resize-none px-3 py-2" />
              </div>
              <button type="submit" className="btn btn-primary mt-4 h-10 w-full text-sm">
                {sent ? '✓' : t.contact.formSubmit}
              </button>
              <p className="mt-3 text-[11px] leading-relaxed text-white/35">{t.contact.formNote}</p>
            </form>
          </Reveal>
        </div>
      </Container>
    </div>
  );
}
