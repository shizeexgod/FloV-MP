'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, Copy, X } from 'lucide-react';

/* ------------------------------------------------------------------ *
 *  Select — компактный кастомный выпадающий список (вместо системного
 *  <select>). Единый стиль для всех «вылезающих списков» в проекте.
 * ------------------------------------------------------------------ */
export interface SelectOption {
  value: string;
  label: string;
  meta?: string;
}

export function Select({
  value,
  options,
  onChange,
  placeholder = '—',
  className = '',
  triggerClassName = '',
  disabled = false,
  ariaLabel,
}: {
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  triggerClassName?: string;
  disabled?: boolean;
  ariaLabel: string;
}) {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const firstOptionRef = useRef<HTMLButtonElement | null>(null);
  const current = options.find((o) => o.value === value);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  useEffect(() => {
    if (open) firstOptionRef.current?.focus();
  }, [open]);

  return (
    <div ref={wrapRef} className={`relative ${className}`}>
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (options.length > 0) setOpen((v) => !v);
        }}
        onKeyDown={(event) => {
          if ((event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') && options.length > 0) {
            event.preventDefault();
            setOpen(true);
          }
        }}
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={open}
        className={`flex h-9 w-full items-center gap-2 rounded-lg border border-white/[0.08] bg-white/[0.03] px-2.5 text-left text-[12px] font-semibold text-white/80 transition-colors hover:border-white/[0.14] hover:bg-white/[0.05] disabled:cursor-not-allowed disabled:opacity-50 ${triggerClassName}`}
      >
        <span className="min-w-0 flex-1 truncate">{current ? current.label : placeholder}</span>
        <ChevronDown aria-hidden="true" className={`h-3.5 w-3.5 shrink-0 text-white/30 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      <div
        role="listbox"
        aria-hidden={!open}
        className={`absolute left-0 top-[calc(100%+0.4rem)] z-20 w-full min-w-[180px] origin-top rounded-xl border border-white/[0.1] p-1.5 shadow-[0_20px_50px_-18px_rgba(0,0,0,0.7)] transition-[opacity,transform] duration-150 ${
          open ? 'pointer-events-auto scale-100 opacity-100' : 'pointer-events-none scale-95 opacity-0'
        }`}
        style={{ backgroundColor: 'var(--panel)' }}
      >
        {options.length === 0 ? (
          <div className="px-2.5 py-2 text-[11.5px] text-white/35">{placeholder}</div>
        ) : (
          options.map((opt, index) => (
            <button
              key={opt.value}
              ref={index === 0 ? firstOptionRef : undefined}
              type="button"
              role="option"
              aria-selected={opt.value === value}
              onClick={() => {
                onChange(opt.value);
                setOpen(false);
              }}
              onKeyDown={(event) => {
                if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                  event.preventDefault();
                  const items = Array.from(
                    wrapRef.current?.querySelectorAll<HTMLButtonElement>('[role="option"]') ?? []
                  );
                  const currentIndex = items.indexOf(event.currentTarget);
                  const direction = event.key === 'ArrowDown' ? 1 : -1;
                  items[(currentIndex + direction + items.length) % items.length]?.focus();
                }
              }}
              className={`flex w-full items-center justify-between gap-2 rounded-lg px-2.5 py-2 text-left text-[12px] font-semibold transition-colors ${
                opt.value === value ? 'bg-brand/10 text-brand' : 'text-white/65 hover:bg-white/[0.05] hover:text-white'
              }`}
            >
              <span className="min-w-0 truncate">{opt.label}</span>
              {opt.meta ? <span className="shrink-0 text-[10px] font-normal text-white/30">{opt.meta}</span> : null}
            </button>
          ))
        )}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 *  Ambient decorative blobs
 * ------------------------------------------------------------------ */
export function AuroraBlobs({ className = '' }: { className?: string }) {
  return (
    <div className={`pointer-events-none absolute inset-0 overflow-hidden ${className}`} aria-hidden>
      <div className="aura-blob left-1/2 top-[-18%] h-[380px] w-[520px] -translate-x-1/2 bg-brand/10" />
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
}: {
  eyebrow: string;
  title: React.ReactNode;
  sub?: React.ReactNode;
  align?: 'center' | 'left';
}) {
  return (
    <div
      className={`max-w-3xl ${align === 'center' ? 'mx-auto text-center' : 'text-left'} ${
        sub ? 'mb-14' : 'mb-12'
      }`}
    >
      <span className="eyebrow">{eyebrow}</span>
      <h2 className="mt-3 text-[1.9rem] font-bold text-white sm:text-4xl sm:leading-[1.12]">{title}</h2>
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
        <Check className="h-3.5 w-3.5 text-ok" />
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
    success: 'border-ok/35 text-ok',
    error: 'border-err/35 text-err',
    info: 'border-brand/35 text-brand',
  };
  return (
    <div className="fixed right-4 top-24 z-[70] max-w-sm animate-slide-down" role="status" aria-live="polite">
      <div
        className={`glass-panel card-edge flex items-start gap-3 rounded-2xl px-4 py-3.5 text-sm font-semibold shadow-glass ${tones[tone]}`}
      >
        <span className="mt-0.5">
          {tone === 'success' ? <Check className="h-4 w-4" /> : tone === 'error' ? <X className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
        </span>
        <span className="flex-1 text-slate-100">{message}</span>
        <button type="button" onClick={onClose} aria-label="Закрыть уведомление" className="text-slate-500 transition-colors hover:text-white">
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
  const titleId = React.useId();
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
      <button
        type="button"
        aria-label="Закрыть диалог"
        className="absolute inset-0 bg-black/70 backdrop-blur-sm animate-fade-in-fast"
        onClick={onClose}
      />
      <div
        className={`glass-panel card-edge relative w-full ${maxWidth} animate-scale-in rounded-3xl p-7 shadow-glass sm:p-8`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label="Закрыть"
          className="absolute right-4 top-4 rounded-lg p-1.5 text-slate-500 transition-colors hover:bg-white/5 hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
        <h3 id={titleId} className="text-xl font-bold text-white">{title}</h3>
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
    brand: 'bg-brand/12 text-brand border-brand/25',
    cyber: 'bg-white/5 text-slate-300 border-white/10',
    emerald: 'bg-ok/12 text-ok border-ok/25',
    violet: 'bg-white/5 text-slate-300 border-white/10',
    amber: 'bg-warn/12 text-warn border-warn/25',
    red: 'bg-err/12 text-err border-err/25',
    slate: 'bg-white/5 text-slate-400 border-white/10',
  };
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider ${tones[tone]} ${className}`}
    >
      {children}
    </span>
  );
}

export function LiveDot({ tone = 'text-ok' }: { tone?: string }) {
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

export function FieldLabel({ children, htmlFor }: { children: React.ReactNode; htmlFor?: string }) {
  return (
    <label htmlFor={htmlFor} className="mb-2 block text-[11px] font-semibold text-white/55">
      {children}
    </label>
  );
}
