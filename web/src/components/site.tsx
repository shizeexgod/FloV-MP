'use client';

import React, { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';

/* ---------------- Scroll reveal (CSS-driven, gentle) ---------------- */
export function Reveal({
  children,
  delay = 0,
  className = '',
  as: Tag = 'div',
}: {
  children: React.ReactNode;
  delay?: number;
  className?: string;
  as?: any;
}) {
  const ref = useRef<HTMLElement | null>(null);
  const [seen, setSeen] = useState(false);

  useEffect(() => {
    // Reveal on mount (two rAF so the CSS transition plays). Never leaves content
    // hidden even if IntersectionObserver never fires (backgrounded tab, etc.).
    let raf1 = 0;
    let raf2 = 0;
    const fallback = window.setTimeout(() => setSeen(true), 250);
    raf1 = requestAnimationFrame(() => {
      raf2 = requestAnimationFrame(() => setSeen(true));
    });
    return () => {
      cancelAnimationFrame(raf1);
      cancelAnimationFrame(raf2);
      clearTimeout(fallback);
    };
  }, []);

  return (
    <Tag
      ref={ref as any}
      className={`reveal ${seen ? 'is-visible' : ''} ${className}`}
      style={{ transitionDelay: `${delay}ms` }}
    >
      {children}
    </Tag>
  );
}

/* ---------------- Layout primitives ---------------- */
export function Container({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`mx-auto w-full max-w-6xl px-5 sm:px-6 ${className}`}>{children}</div>;
}

export function Section({
  id,
  children,
  className = '',
  bordered = true,
}: {
  id?: string;
  children: React.ReactNode;
  className?: string;
  bordered?: boolean;
}) {
  return (
    <section id={id} className={`scroll-mt-20 ${bordered ? 'border-b border-white/[0.07]' : ''} ${className}`}>
      <Container className="py-16 sm:py-20">{children}</Container>
    </section>
  );
}

export function PageHero({
  eyebrow,
  title,
  sub,
}: {
  eyebrow?: string;
  title: React.ReactNode;
  sub?: React.ReactNode;
}) {
  return (
    <div className="border-b border-white/[0.07]">
      <Container className="py-16 sm:py-20">
        <Reveal>
          {eyebrow ? <span className="eyebrow">{eyebrow}</span> : null}
          <h1 className="mt-3 max-w-3xl text-3xl font-semibold tracking-tight sm:text-[2.6rem] sm:leading-[1.1]">
            {title}
          </h1>
          {sub ? <p className="mt-4 max-w-2xl text-[15px] leading-relaxed text-white/55">{sub}</p> : null}
        </Reveal>
      </Container>
    </div>
  );
}

export function SectionHeading({
  eyebrow,
  title,
  sub,
}: {
  eyebrow?: string;
  title: React.ReactNode;
  sub?: React.ReactNode;
}) {
  return (
    <Reveal className="mb-10 max-w-2xl">
      {eyebrow ? <span className="eyebrow">{eyebrow}</span> : null}
      <h2 className="mt-3 text-2xl font-semibold tracking-tight sm:text-[1.9rem]">{title}</h2>
      {sub ? <p className="mt-3 text-[14px] leading-relaxed text-white/50">{sub}</p> : null}
    </Reveal>
  );
}

/* ---------------- Buttons as links ---------------- */
export function BtnLink({
  href,
  children,
  variant = 'primary',
  external = false,
  className = '',
  arrow = false,
}: {
  href: string;
  children: React.ReactNode;
  variant?: 'primary' | 'secondary' | 'ghost';
  external?: boolean;
  className?: string;
  arrow?: boolean;
}) {
  const cls = `btn btn-${variant} ${className}`;
  const inner = (
    <>
      {children}
      {arrow ? <ArrowRight className="h-4 w-4" /> : null}
    </>
  );
  if (external) {
    return (
      <a href={href} target="_blank" rel="noopener noreferrer" className={cls}>
        {inner}
      </a>
    );
  }
  return (
    <Link href={href} className={cls}>
      {inner}
    </Link>
  );
}

/* ---------------- Count-up number ---------------- */
export function CountUp({
  value,
  suffix = '',
  decimals = 0,
  duration = 1200,
}: {
  value: number;
  suffix?: string;
  decimals?: number;
  duration?: number;
}) {
  const ref = useRef<HTMLSpanElement | null>(null);
  const [n, setN] = useState(0);

  useEffect(() => {
    let raf = 0;
    let start = 0;
    const startAt = window.setTimeout(() => {
      const tick = (ts: number) => {
        if (!start) start = ts;
        const p = Math.min(1, (ts - start) / duration);
        const eased = 1 - Math.pow(1 - p, 3);
        setN(value * eased);
        if (p < 1) raf = requestAnimationFrame(tick);
        else setN(value);
      };
      raf = requestAnimationFrame(tick);
    }, 120);
    return () => {
      clearTimeout(startAt);
      cancelAnimationFrame(raf);
    };
  }, [value, duration]);

  return (
    <span ref={ref}>
      {n.toLocaleString('ru-RU', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })}
      {suffix}
    </span>
  );
}
