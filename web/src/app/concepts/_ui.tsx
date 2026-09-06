'use client';

import React, { useEffect, useRef, useState } from 'react';

/* ------------------------------------------------------------------ *
 *  Reveal-on-scroll (dependency-free stand-in for Framer Motion)
 * ------------------------------------------------------------------ */
export function Reveal({
  children,
  delay = 0,
  y = 14,
  className = '',
  as: Tag = 'div',
}: {
  children: React.ReactNode;
  delay?: number;
  y?: number;
  className?: string;
  as?: any;
}) {
  const ref = useRef<HTMLElement | null>(null);
  const [shown, setShown] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (typeof IntersectionObserver === 'undefined') {
      setShown(true);
      return;
    }
    const io = new IntersectionObserver(
      ([e]) => {
        if (e.isIntersecting) {
          setShown(true);
          io.disconnect();
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -8% 0px' }
    );
    io.observe(el);
    return () => io.disconnect();
  }, []);

  return (
    <Tag
      ref={ref as any}
      className={className}
      style={{
        opacity: shown ? 1 : 0,
        transform: shown ? 'none' : `translateY(${y}px)`,
        transition: `opacity .6s cubic-bezier(.2,.7,.2,1) ${delay}ms, transform .6s cubic-bezier(.2,.7,.2,1) ${delay}ms`,
        willChange: 'opacity, transform',
      }}
    >
      {children}
    </Tag>
  );
}

/* ------------------------------------------------------------------ *
 *  Count-up number
 * ------------------------------------------------------------------ */
export function CountUp({
  value,
  decimals = 0,
  duration = 1400,
  className = '',
}: {
  value: number;
  decimals?: number;
  duration?: number;
  className?: string;
}) {
  const ref = useRef<HTMLSpanElement | null>(null);
  const [n, setN] = useState(0);
  const started = useRef(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(([e]) => {
      if (!e.isIntersecting || started.current) return;
      started.current = true;
      const t0 = performance.now();
      const tick = (t: number) => {
        const p = Math.min(1, (t - t0) / duration);
        const eased = 1 - Math.pow(1 - p, 3);
        setN(value * eased);
        if (p < 1) requestAnimationFrame(tick);
        else setN(value);
      };
      requestAnimationFrame(tick);
    }, { threshold: 0.4 });
    io.observe(el);
    return () => io.disconnect();
  }, [value, duration]);

  return (
    <span ref={ref} className={className}>
      {n.toLocaleString('ru-RU', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })}
    </span>
  );
}

/* ------------------------------------------------------------------ *
 *  Charts — hand-rolled inline SVG, colour-parametrised
 * ------------------------------------------------------------------ */
function toPath(data: number[], w: number, h: number, pad = 4) {
  const n = data.length;
  const stepX = (w - pad * 2) / (n - 1);
  return data.map((v, i) => {
    const x = pad + i * stepX;
    const y = pad + (1 - v) * (h - pad * 2);
    return `${i === 0 ? 'M' : 'L'}${x.toFixed(2)} ${y.toFixed(2)}`;
  }).join(' ');
}

export function AreaChart({
  data,
  color = '#ff1493',
  height = 120,
  fill = 0.18,
  grid = true,
  id,
}: {
  data: number[];
  color?: string;
  height?: number;
  fill?: number;
  grid?: boolean;
  id?: string;
}) {
  const w = 320;
  const h = height;
  const gid = id || `ac-${Math.random().toString(36).slice(2, 8)}`;
  const line = toPath(data, w, h);
  const area = `${line} L${w - 4} ${h - 4} L4 ${h - 4} Z`;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className="w-full" style={{ height }}>
      <defs>
        <linearGradient id={gid} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={fill} />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      {grid &&
        [0.25, 0.5, 0.75].map((g) => (
          <line key={g} x1="4" x2={w - 4} y1={4 + g * (h - 8)} y2={4 + g * (h - 8)} stroke="currentColor" strokeOpacity="0.08" strokeWidth="1" />
        ))}
      <path d={area} fill={`url(#${gid})`} />
      <path d={line} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function LineChart({
  series,
  height = 120,
  grid = true,
}: {
  series: { data: number[]; color: string }[];
  height?: number;
  grid?: boolean;
}) {
  const w = 320;
  const h = height;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className="w-full" style={{ height }}>
      {grid &&
        [0.25, 0.5, 0.75].map((g) => (
          <line key={g} x1="4" x2={w - 4} y1={4 + g * (h - 8)} y2={4 + g * (h - 8)} stroke="currentColor" strokeOpacity="0.08" strokeWidth="1" />
        ))}
      {series.map((s, i) => (
        <path key={i} d={toPath(s.data, w, h)} fill="none" stroke={s.color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      ))}
    </svg>
  );
}

export function Bars({
  data,
  color = '#ff1493',
  height = 120,
  labels,
}: {
  data: number[];
  color?: string;
  height?: number;
  labels?: string[];
}) {
  const w = 320;
  const h = height;
  const n = data.length;
  const gap = 6;
  const bw = (w - gap * (n - 1)) / n;
  return (
    <svg viewBox={`0 0 ${w} ${h + (labels ? 16 : 0)}`} className="w-full" style={{ height: h + (labels ? 16 : 0) }}>
      {data.map((v, i) => {
        const bh = Math.max(2, v * (h - 6));
        return (
          <g key={i}>
            <rect x={i * (bw + gap)} y={h - bh} width={bw} height={bh} rx={Math.min(4, bw / 2)} fill={color} fillOpacity={0.28 + v * 0.6} />
            {labels && (
              <text x={i * (bw + gap) + bw / 2} y={h + 12} textAnchor="middle" fontSize="9" fill="currentColor" fillOpacity="0.4" fontFamily="ui-monospace, monospace">
                {labels[i]}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}

export function Donut({
  value,
  size = 132,
  stroke = 12,
  color = '#ff1493',
  track = 'rgba(255,255,255,0.08)',
  children,
}: {
  value: number; // 0..1
  size?: number;
  stroke?: number;
  color?: string;
  track?: string;
  children?: React.ReactNode;
}) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  return (
    <div className="relative inline-grid place-items-center" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={track} strokeWidth={stroke} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke={color}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={c * (1 - value)}
          style={{ transition: 'stroke-dashoffset 1s cubic-bezier(.2,.7,.2,1)' }}
        />
      </svg>
      <div className="absolute inset-0 grid place-items-center">{children}</div>
    </div>
  );
}

export function Sparkline({ data, color = '#ff1493', width = 84, height = 26 }: { data: number[]; color?: string; width?: number; height?: number }) {
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} preserveAspectRatio="none">
      <path d={toPath(data, width, height, 2)} fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ------------------------------------------------------------------ *
 *  Concept switcher — floating pill shown on every concept page
 * ------------------------------------------------------------------ */
export function ConceptSwitch({ current }: { current: 'saas' | 'gta' | 'hybrid' }) {
  const items: { id: 'saas' | 'gta' | 'hybrid'; label: string }[] = [
    { id: 'saas', label: '01 · SaaS' },
    { id: 'gta', label: '02 · GTA' },
    { id: 'hybrid', label: '03 · Hybrid' },
  ];
  return (
    <div className="fixed bottom-5 left-1/2 z-[120] -translate-x-1/2">
      <div className="flex items-center gap-1 rounded-full border border-white/12 bg-black/70 p-1 backdrop-blur-xl shadow-[0_12px_40px_-12px_rgba(0,0,0,0.8)]">
        <a href="/concepts" className="px-3 py-1.5 text-[11px] font-semibold text-white/50 transition hover:text-white">
          ← Все
        </a>
        {items.map((it) => (
          <a
            key={it.id}
            href={`/concepts/${it.id}`}
            className={`rounded-full px-3 py-1.5 font-mono text-[11px] font-semibold transition ${
              current === it.id ? 'bg-white text-black' : 'text-white/55 hover:text-white'
            }`}
          >
            {it.label}
          </a>
        ))}
      </div>
    </div>
  );
}
