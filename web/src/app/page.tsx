'use client';

import React from 'react';
import Image from 'next/image';
import { Check, Code2, Download, Gamepad2, Layers3, MessageCircle, Monitor, Settings, Shield, Terminal } from 'lucide-react';
import { BtnLink, Container, Reveal, Section } from '@/components/site';
import { useT } from '@/lib/i18n';

const FEATURE_ICONS = [Layers3, Shield, Terminal, Gamepad2, Download, Code2, Monitor, Settings];

function EditorialHeading({ index, title, text, align = 'left' }: { index?: string; title: React.ReactNode; text?: React.ReactNode; align?: 'left' | 'center' }) {
  return (
    <div className={align === 'center' ? 'mx-auto max-w-2xl text-center' : 'max-w-xl'}>
      {index ? <div className="font-mono text-[11px] font-semibold tracking-[0.18em] text-brand">{index}</div> : null}
      <h2 className="mt-4 text-[1.75rem] font-bold leading-[1.15] tracking-[-0.045em] text-white sm:text-[2.35rem]">{title}</h2>
      {text ? <p className="mt-5 text-[14px] leading-relaxed text-white/[0.54] sm:text-[15px]">{text}</p> : null}
    </div>
  );
}

function DashboardMockup({ alt }: { alt: string }) {
  return (
    <div className="dashboard-stage" >
      <div className="dashboard-stage__frame">
        <div className="flex h-9 items-center gap-1.5 border-b border-white/[0.07] px-3" aria-hidden="true">
          <i className="h-1.5 w-1.5 rounded-full bg-white/[0.22]" /><i className="h-1.5 w-1.5 rounded-full bg-white/[0.14]" /><i className="h-1.5 w-1.5 rounded-full bg-white/[0.1]" />
          <span className="ml-3 font-mono text-[9px] text-white/[0.28]">flovmp.ru/dashboard</span>
        </div>
        <Image src="/media/shot-dashboard-overview.png" alt={alt} width={1440} height={805} sizes="(max-width: 1024px) 100vw, 62vw" className="h-auto w-full" />
      </div>
    </div>
  );
}

export default function HomePage() {
  const t = useT();
  const leadFeatures = t.home.features.slice(0, 2);
  const compactFeatures = t.home.features.slice(2);
  const copy = t.home.marketing;

  return (
    <div className="marketing-home">
      <section className="marketing-hero">
        <Container className="relative grid min-h-[min(790px,calc(100svh-64px))] items-center gap-6 py-16 lg:grid-cols-[.72fr_1.28fr] lg:py-20">
          <div className="hero-copy relative z-10 max-w-[560px]">
            <Reveal variant="left">
              <p className="font-mono text-[11px] font-semibold tracking-[0.18em] text-brand">{t.home.heroKicker}</p>
              <h1 className="mt-6 text-[2.6rem] font-bold leading-[1.06] tracking-[-0.06em] text-white sm:text-[3.6rem] xl:text-[4.5rem]">
                <span className="block">{t.home.h1a}</span>
                <span className="block text-brand">{t.home.h1accent}</span>
                <span className="block">{t.home.h1b}</span>
              </h1>
            </Reveal>
            <Reveal delay={80} variant="rise"><p className="mt-6 max-w-[515px] text-[14px] leading-[1.75] text-white/[0.57] sm:text-[15px]">{t.home.sub}</p></Reveal>
            <Reveal delay={140} variant="rise">
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <BtnLink href="/pricing" variant="primary" arrow className="h-12 px-5 text-sm">{t.home.ctaPrimary}</BtnLink>
                <BtnLink href="/docs" variant="ghost" className="h-12 px-5 text-sm"><Terminal aria-hidden="true" className="h-4 w-4" />{t.home.ctaSecondary}</BtnLink>
              </div>
            </Reveal>
            <Reveal delay={190} variant="fade"><p className="mt-7 max-w-[490px] border-l border-brand/60 pl-3.5 text-[11px] leading-relaxed text-white/[0.38]">{t.home.trustLine}</p></Reveal>
          </div>

          <Reveal delay={100} variant="scale" className="hero-keyart-wrap">
            <div className="hero-keyart">
              <Image src="/branding/recent-logo.jpg" alt={copy.keyartAlt} fill priority sizes="(max-width: 1024px) 100vw, 58vw" className="object-cover object-center" />
              <div className="hero-keyart__shade" aria-hidden="true" />
              <div className="hero-keyart__line" aria-hidden="true" />
              <div className="hero-keyart__meta"><span>{copy.heroMeta[0]}</span><span>{copy.heroMeta[1]}</span></div>
            </div>
          </Reveal>
        </Container>
      </section>

      <Section id="start" className="marketing-section">
        <div className="grid gap-10 lg:grid-cols-[.68fr_1.32fr] lg:items-end">
          <Reveal variant="left"><EditorialHeading index={copy.sectionLabels.start} title={t.home.flowTitle} text={t.home.flowSub} /></Reveal>
          <div className="grid gap-5 sm:grid-cols-2">
            {t.home.flow.map((step, index) => (
              <Reveal key={step.t} delay={index * 90} variant="rise">
                <article className="journey-panel">
                  <div className="journey-panel__media">
                    <Image
                      src={index === 0 ? '/media/shot-dashboard-projects.png' : '/media/shot-dashboard-console.png'}
                      alt=""
                      width={1440}
                      height={805}
                      sizes="(max-width: 640px) 100vw, 42vw"
                      className="h-auto w-full"
                    />
                    <span className="journey-panel__number">0{index + 1}</span>
                  </div>
                  <div className="journey-panel__body">
                    <h3>{step.t}</h3>
                    <p>{step.d}</p>
                    <BtnLink href={step.href} variant="link" arrow className="mt-5 text-[13px]">{step.cta}</BtnLink>
                  </div>
                </article>
              </Reveal>
            ))}
          </div>
        </div>
      </Section>

      <Section id="dashboard" className="marketing-section">
        <div className="grid gap-12 lg:grid-cols-[.64fr_1.36fr] lg:items-center">
          <Reveal variant="left">
            <EditorialHeading index={copy.sectionLabels.control} title={copy.dashboardTitle} text={t.home.pillars[1].d} />
            <ul className="mt-8 space-y-3 text-[13px] text-white/[0.56]">
              {copy.dashboardChecks.map((item) => <li key={item} className="flex gap-3"><Check aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-brand" />{item}</li>)}
            </ul>
            <BtnLink href="/dashboard" variant="secondary" arrow className="mt-8">{copy.dashboardCta}</BtnLink>
          </Reveal>
          <Reveal delay={100} variant="scale"><DashboardMockup alt={copy.dashboardAlt} /></Reveal>
        </div>
      </Section>

      <Section id="platform" className="marketing-section">
        <div className="space-y-24 lg:space-y-32">
          {t.home.pillars.map((pillar, index) => (
            <div key={pillar.n} className={`grid gap-10 lg:grid-cols-2 lg:items-center lg:gap-20 ${index % 2 ? 'lg:[&>*:first-child]:order-2' : ''}`}>
              <Reveal variant={index % 2 ? 'right' : 'left'}><EditorialHeading index={`0${index + 3} / ${copy.sectionLabels.platform[index]}`} title={pillar.t} text={pillar.d} /></Reveal>
              <Reveal delay={90} variant="scale">
                <div className={`platform-visual platform-visual--${index}`}>
                  <div className="platform-visual__top">
                    {index === 0 ? <Monitor aria-hidden="true" className="h-6 w-6 text-brand" /> : index === 1 ? <Settings aria-hidden="true" className="h-6 w-6 text-brand" /> : <Shield aria-hidden="true" className="h-6 w-6 text-brand" />}
                    <p>{copy.platformMeta[index]}</p>
                  </div>
                  <ul className="platform-visual__facts">
                    {copy.platformDetails[index].map((item, factIndex) => <li key={item}><span>0{factIndex + 1}</span>{item}</li>)}
                  </ul>
                </div>
              </Reveal>
            </div>
          ))}
        </div>
      </Section>

      <Section id="features" className="marketing-section">
        <Reveal><EditorialHeading index={copy.sectionLabels.included} title={t.home.featuresTitle} text={t.home.featuresSub} /></Reveal>
        <div className="capability-list mt-12">
          {[...leadFeatures, ...compactFeatures].map((feature, index) => {
            const Icon = FEATURE_ICONS[index] ?? Layers3;
            return (
              <Reveal key={feature.t} delay={Math.min(index * 38, 180)} variant="rise">
                <article className="capability-row">
                  <span className="capability-row__number">{String(index + 1).padStart(2, '0')}</span>
                  <Icon aria-hidden="true" className="h-[18px] w-[18px] shrink-0 text-brand" />
                  <div className="min-w-0"><h3>{feature.t}</h3><p>{feature.d}</p></div>
                </article>
              </Reveal>
            );
          })}
        </div>
        <Reveal><BtnLink href="/features" variant="link" arrow className="mt-10 text-[13px]">{copy.allFeatures}</BtnLink></Reveal>
      </Section>

      <Section id="developers" className="marketing-section">
        <div className="grid gap-10 lg:grid-cols-[.78fr_1.22fr] lg:items-center">
          <Reveal variant="left"><EditorialHeading index={copy.sectionLabels.sdk} title={copy.devTitle} text={copy.devText} /><BtnLink href="/docs" variant="secondary" arrow className="mt-8">{copy.docsCta}</BtnLink></Reveal>
          <Reveal delay={100} variant="scale"><pre className="code-window" {...{ "aria-label": copy.codeSampleLabel }}><code><span className="code-window__muted"># server.toml</span>{'\n'}name = <span className="code-window__string">&quot;My GTA V Project&quot;</span>{'\n'}port = <span className="code-window__number">7788</span>{'\n'}license_key = <span className="code-window__string">&quot;FLV-••••-••••-••••&quot;</span>{'\n'}cdn_url = <span className="code-window__string">&quot;https://cdn.example.ru&quot;</span>{'\n\n'}<span className="code-window__muted"># C# / .NET 8 gamemode</span>{'\n'}await server.StartAsync();</code></pre></Reveal>
        </div>
      </Section>

      <Section id="license" bordered={false} className="marketing-section">
        <Reveal><div className="license-band"><div><p className="font-mono text-[11px] font-semibold tracking-[0.18em] text-brand">{copy.sectionLabels.license}</p><h2>{copy.licenseTitle}</h2><p>{copy.licenseText}</p></div><div className="license-band__action"><strong translate="no">{copy.licensePrice}</strong><BtnLink href="/pricing" variant="primary" arrow>{copy.licenseCta}</BtnLink></div></div></Reveal>
      </Section>
    </div>
  );
}
