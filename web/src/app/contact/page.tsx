'use client';

import React, { useState } from 'react';
import { Mail, Send } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';
import { LEGAL } from '@/lib/legal';

export default function ContactPage() {
  const t = useT();
  const [name, setName] = useState('');
  const [contact, setContact] = useState('');
  const [about, setAbout] = useState('');

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const text = [
      'Заявка с сайта FloV:MP',
      `Имя / ник: ${name || '—'}`,
      `Контакт для ответа: ${contact || '—'}`,
      `О проекте: ${about || '—'}`,
    ].join('\n');
    const url = `https://t.me/share/url?url=${encodeURIComponent(LEGAL.siteUrl)}&text=${encodeURIComponent(text)}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  };

  return (
    <div>
      <PageHero eyebrow={t.nav.contact} title={t.contact.title} sub={t.contact.sub} />

      <Container className="py-14 sm:py-20">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_1fr]">
          <div className="space-y-4">
            <Reveal>
              <a
                href={LEGAL.telegramUrl}
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
                  <span className="mt-2 inline-block font-mono text-[12px] text-brand">{LEGAL.telegramHandle}</span>
                </div>
              </a>
            </Reveal>
            <Reveal delay={70}>
              <a href={`mailto:${LEGAL.email}`} className="card card-hover flex items-start gap-4 p-6">
                <span className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-brand">
                  <Mail className="h-4 w-4" />
                </span>
                <div>
                  <div className="text-[14px] font-semibold text-white">{t.contact.dsTitle}</div>
                  <p className="mt-1 text-[12.5px] leading-relaxed text-white/50">{t.contact.dsDesc}</p>
                  <span className="mt-2 inline-block font-mono text-[12px] text-brand">{LEGAL.email}</span>
                </div>
              </a>
            </Reveal>
          </div>

          <Reveal delay={120}>
            <form onSubmit={submit} className="card p-6">
              <h2 className="text-[15px] font-semibold text-white">{t.contact.formTitle}</h2>
              <div className="mt-4 space-y-3">
                <input
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder={t.contact.formName}
                  className="field h-10 px-3"
                />
                <input
                  required
                  value={contact}
                  onChange={(e) => setContact(e.target.value)}
                  placeholder={t.contact.formContact}
                  className="field h-10 px-3"
                />
                <textarea
                  required
                  rows={4}
                  value={about}
                  onChange={(e) => setAbout(e.target.value)}
                  placeholder={t.contact.formProject}
                  className="field resize-none px-3 py-2"
                />
              </div>
              <button type="submit" className="btn btn-primary mt-4 h-10 w-full text-sm">
                <Send className="h-4 w-4" />
                {t.contact.formSubmit}
              </button>
              <p className="mt-3 text-[11px] leading-relaxed text-white/35">{t.contact.formNote}</p>
            </form>
          </Reveal>
        </div>
      </Container>
    </div>
  );
}
