'use client';

import React, { useState } from 'react';
import { Modal, FieldLabel, Spinner } from '@/components/ui';
import { Plus, Tag } from 'lucide-react';

interface NewServerModalProps {
  open: boolean;
  onClose: () => void;
  projectId: number;
  onCreated: () => void;
}

const ENVIRONMENTS = [
  { id: 'production', label: 'Production', badge: 'PROD', color: 'border-emeraldx/40 bg-emeraldx/15 text-emeraldx' },
  { id: 'development', label: 'Development', badge: 'DEV', color: 'border-cyber/40 bg-cyber/15 text-cyber' },
  { id: 'test', label: 'Testing', badge: 'TEST', color: 'border-amber-500/40 bg-amber-500/15 text-amber-300' },
  { id: 'staging', label: 'Staging', badge: 'STAGE', color: 'border-violetx/40 bg-violetx/15 text-violetx' },
] as const;

export function NewServerModal({ open, onClose, projectId, onCreated }: NewServerModalProps) {
  const [name, setName] = useState('');
  const [label, setLabel] = useState('DEV');
  const [environment, setEnvironment] = useState<'production' | 'development' | 'test' | 'staging'>('development');
  const [ip, setIp] = useState('127.0.0.1');
  const [port, setPort] = useState(7788);
  const [unlimitedSlots, setUnlimitedSlots] = useState(true);
  const [slotLimit, setSlotLimit] = useState(128);
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await fetch(`/api/v1/projects/${projectId}/servers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: name.trim(),
          label: label.trim() || null,
          environment,
          ip: ip.trim() || '127.0.0.1',
          port: Number(port) || 7788,
          maxPlayers: unlimitedSlots ? 5000 : Number(slotLimit),
          slotLimit: unlimitedSlots ? null : Number(slotLimit),
          notes: notes.trim() || null,
        }),
      });

      const data = await res.json();
      if (!res.ok || !data.success) {
        throw new Error(data.error || 'Ошибка добавления сервера');
      }

      onCreated();
      onClose();
      // Reset form
      setName('');
      setLabel('DEV');
      setEnvironment('development');
    } catch (err: any) {
      setError(err.message || 'Произошла ошибка при создании сервера');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Добавить сервер проекта"
      description="Подключение нового игрового сервера (Production, Dev или Test) к проекту"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-300">
            {error}
          </div>
        )}

        <div>
          <FieldLabel>Название инстанса сервера</FieldLabel>
          <input
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Например: Тестовый стенд разработки #1"
            className="field h-11 px-4 text-sm"
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <FieldLabel>
              <span className="flex items-center gap-1">
                <Tag className="h-3 w-3 text-brand" />
                Визуальный префикс
              </span>
            </FieldLabel>
            <input
              value={label}
              onChange={(e) => setLabel(e.target.value.toUpperCase())}
              placeholder="DEV, TEST, PROD"
              className="field h-11 px-4 font-mono text-sm uppercase"
            />
          </div>
          <div>
            <FieldLabel>Порт UDP</FieldLabel>
            <input
              type="number"
              value={port}
              onChange={(e) => setPort(Number(e.target.value))}
              placeholder="7788"
              className="field h-11 px-4 font-mono text-sm"
            />
          </div>
        </div>

        <div>
          <FieldLabel>Окружение (Environment)</FieldLabel>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {ENVIRONMENTS.map((env) => (
              <button
                type="button"
                key={env.id}
                onClick={() => {
                  setEnvironment(env.id as any);
                  if (!label || ['PROD', 'DEV', 'TEST', 'STAGE'].includes(label)) {
                    setLabel(env.badge);
                  }
                }}
                className={`rounded-xl border p-2.5 text-center transition-all ${
                  environment === env.id
                    ? `${env.color} ring-1 ring-white/20`
                    : 'border-white/10 bg-white/[0.02] text-slate-400 hover:bg-white/[0.05]'
                }`}
              >
                <div className="font-mono text-xs font-bold">[{env.badge}]</div>
                <div className="mt-0.5 text-[10px] truncate">{env.label}</div>
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <FieldLabel>IP адрес (Bind)</FieldLabel>
            <input
              value={ip}
              onChange={(e) => setIp(e.target.value)}
              placeholder="127.0.0.1"
              className="field h-11 px-4 font-mono text-sm"
            />
          </div>
          <div>
            <FieldLabel>Лимит слотов</FieldLabel>
            <div className="flex items-center gap-2 mt-1">
              <input
                type="number"
                disabled={unlimitedSlots}
                value={slotLimit}
                onChange={(e) => setSlotLimit(Number(e.target.value))}
                className="field h-11 w-24 px-3 font-mono text-sm disabled:opacity-40"
              />
              <button
                type="button"
                onClick={() => setUnlimitedSlots(!unlimitedSlots)}
                className={`text-xs px-2.5 py-2 rounded-xl border transition ${
                  unlimitedSlots
                    ? 'border-emeraldx/40 bg-emeraldx/15 text-emeraldx font-bold'
                    : 'border-white/10 bg-white/5 text-slate-400'
                }`}
              >
                {unlimitedSlots ? 'Безлимит (до 5000+)' : 'Ограничен'}
              </button>
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-xs text-slate-400 transition hover:text-white"
          >
            Отмена
          </button>
          <button
            type="submit"
            disabled={loading}
            className="btn btn-primary h-10 px-5 text-xs font-bold disabled:opacity-50"
          >
            {loading ? <Spinner className="h-4 w-4" /> : <Plus className="h-4 w-4 mr-1.5" />}
            Создать сервер
          </button>
        </div>
      </form>
    </Modal>
  );
}
