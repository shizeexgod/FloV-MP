'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Check, Copy, X } from 'lucide-react';

/* ------------------------------------------------------------------ *
 *  Ambient decorative blobs
 * ------------------------------------------------------------------ */
export function AuroraBlobs({ className = '' }: { className?: string }) {
  return (
    <div className={`pointer-events-none absolute inset-0 overflow-hidden ${className}`} aria-hidden>
      <div className="aura-blob left-[-8%] top-[-12%] h-[420px] w-[420px] bg-brand/25" />
      <div className="aura-blob right-[-6%] top-[6%] h-[380px] w-[380px] bg-violetx/20" />
      <div className="aura-blob left-1/3 bottom-[-20%] h-[460px] w-[460px] bg-cyber/12" />
    </div>
  );
}

/* ------------------------------------------------------------------ *
 *  Section wrapper + eyebrow heading
 * ------------------------------------------------------------------ */
export function Section({
  id,
  children,
  className = '',
  bleed = false,
}: {
  id?: string;
  children: React.ReactNode;
  className?: string;
  bleed?: boolean;
}) {
  return (
    <section
      id={id}
      className={`relative scroll-mt-24 px-4 sm:px-6 lg:px-8 ${
        bleed ? 'border-y border-white/[0.06] bg-ink-900/40' : ''
      } ${className}`}
    >
      <div className="mx-auto w-full max-w-7xl">{children}</div>
    </section>
  );
}

export function SectionHeading({
  eyebrow,
  title,
  sub,
  align = 'center',
  accent = 'brand',
}: {
  eyebrow: string;
  title: React.ReactNode;
  sub?: React.ReactNode;
  align?: 'center' | 'left';
  accent?: 'brand' | 'cyber';
}) {
  return (
    <div
      className={`max-w-3xl ${align === 'center' ? 'mx-auto text-center' : 'text-left'} ${
        sub ? 'mb-14' : 'mb-12'
      }`}
    >
      <span className={`eyebrow ${accent === 'brand' ? 'text-brand' : 'text-cyber'}`}>{eyebrow}</span>
      <h2 className="mt-3 text-3xl font-black text-white sm:text-[2.6rem] sm:leading-[1.08]">{title}</h2>
      {sub ? <p className="mt-4 text-[15px] leading-relaxed text-slate-400">{sub}</p> : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 *  Copy-to-clipboard button with tooltip feedback
 * ------------------------------------------------------------------ */
export function CopyButton({
  value,
  label,
  className = '',
  size = 'md',
}: {
  value: string;
  label?: string;
  className?: string;
  size?: 'sm' | 'md';
}) {
  const [copied, setCopied] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const copy = useCallback(() => {
    navigator.clipboard?.writeText(value).catch(() => {});
    setCopied(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setCopied(false), 1800);
  }, [value]);

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  const pad = size === 'sm' ? 'h-8 px-2.5 text-[11px]' : 'h-10 px-3 text-xs';

  return (
    <button
      type="button"
      onClick={copy}
      className={`btn btn-ghost relative ${pad} font-semibold ${className}`}
      title="Скопировать"
    >
      {copied ? (
        <Check className="h-3.5 w-3.5 text-emeraldx" />
      ) : (
        <Copy className="h-3.5 w-3.5" />
      )}
      {label ? <span>{copied ? 'Скопировано' : label}</span> : null}
    </button>
  );
}

/* ------------------------------------------------------------------ *
 *  Toast
 * ------------------------------------------------------------------ */
export type ToastTone = 'success' | 'error' | 'info';

export function Toast({
  message,
  tone = 'success',
  onClose,
}: {
  message: string;
  tone?: ToastTone;
  onClose: () => void;
}) {
  const tones: Record<ToastTone, string> = {
    success: 'border-emeraldx/40 text-emeraldx',
    error: 'border-red-500/40 text-red-400',
    info: 'border-cyber/40 text-cyber',
  };
  return (
    <div className="fixed right-4 top-24 z-[70] max-w-sm animate-slide-down">
      <div
        className={`glass-panel card-edge flex items-start gap-3 rounded-2xl px-4 py-3.5 text-sm font-semibold shadow-glass ${tones[tone]}`}
      >
        <span className="mt-0.5">
          {tone === 'success' ? <Check className="h-4 w-4" /> : tone === 'error' ? <X className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
        </span>
        <span className="flex-1 text-slate-100">{message}</span>
        <button onClick={onClose} className="text-slate-500 transition hover:text-white">
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}

export function useToast() {
  const [toast, setToast] = useState<{ message: string; tone: ToastTone } | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const show = useCallback((message: string, tone: ToastTone = 'success') => {
    setToast({ message, tone });
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setToast(null), 4200);
  }, []);

  const node = toast ? (
    <Toast message={toast.message} tone={toast.tone} onClose={() => setToast(null)} />
  ) : null;

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  return { show, node };
}

/* ------------------------------------------------------------------ *
 *  Modal shell
 * ------------------------------------------------------------------ */
export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  maxWidth = 'max-w-lg',
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  maxWidth?: string;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center p-4">
      <div
        className="absolute inset-0 bg-black/70 backdrop-blur-sm animate-fade-in-fast"
        onClick={onClose}
      />
      <div
        className={`glass-panel card-edge relative w-full ${maxWidth} animate-scale-in rounded-3xl p-7 shadow-glass sm:p-8`}
      >
        <button
          onClick={onClose}
          className="absolute right-4 top-4 rounded-lg p-1.5 text-slate-500 transition hover:bg-white/5 hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
        <h3 className="text-xl font-bold text-white">{title}</h3>
        {description ? <p className="mt-1.5 text-xs leading-relaxed text-slate-400">{description}</p> : null}
        <div className="mt-6">{children}</div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 *  Small primitives
 * ------------------------------------------------------------------ */
export function Badge({
  children,
  tone = 'brand',
  className = '',
}: {
  children: React.ReactNode;
  tone?: 'brand' | 'cyber' | 'emerald' | 'violet' | 'amber' | 'red' | 'slate';
  className?: string;
}) {
  const tones: Record<string, string> = {
    brand: 'bg-brand/15 text-brand border-brand/30',
    cyber: 'bg-cyber/15 text-cyber border-cyber/30',
    emerald: 'bg-emeraldx/15 text-emeraldx border-emeraldx/30',
    violet: 'bg-violetx/15 text-violetx border-violetx/30',
    amber: 'bg-amber-500/15 text-amber-400 border-amber-500/30',
    red: 'bg-red-500/15 text-red-400 border-red-500/30',
    slate: 'bg-white/5 text-slate-300 border-white/10',
  };
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider ${tones[tone]} ${className}`}
    >
      {children}
    </span>
  );
}

export function LiveDot({ tone = 'text-emeraldx' }: { tone?: string }) {
  return <span className={`status-dot ${tone}`} />;
}

export function Spinner({ className = 'h-5 w-5' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} viewBox="0 0 24 24" fill="none">
      <circle className="opacity-20" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-90" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
    </svg>
  );
}

export function FieldLabel({ children }: { children: React.ReactNode }) {
  return (
    <label className="mb-2 block font-mono text-[11px] font-semibold uppercase tracking-wider text-slate-400">
      {children}
    </label>
  );
}
