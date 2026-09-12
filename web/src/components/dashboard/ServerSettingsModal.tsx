'use client';

import React, { useState, useEffect } from 'react';
import { Modal, FieldLabel, Spinner } from '@/components/ui';
import { Sliders, Check, Shield, Cpu, Tag } from 'lucide-react';
import type { ServerInstance } from './_ctx';

interface ServerSettingsModalProps {
  open: boolean;
  onClose: () => void;
  server: ServerInstance | null;
  onSaved: () => void;
}

const ENVIRONMENTS = [
  { id: 'production', label: 'Production', badge: 'PROD', color: 'border-emeraldx/40 bg-emeraldx/15 text-emeraldx' },
  { id: 'development', label: 'Development', badge: 'DEV', color: 'border-cyber/40 bg-cyber/15 text-cyber' },
  { id: 'test', label: 'Testing', badge: 'TEST', color: 'border-amber-500/40 bg-amber-500/15 text-amber-300' },
  { id: 'staging', label: 'Staging', badge: 'STAGE', color: 'border-violetx/40 bg-violetx/15 text-violetx' },
] as const;

const PRESET_PREFIXES = ['PROD', 'DEV', 'TEST', 'DEV-1', 'TEST-PVP', 'STAGE', 'EVENT'];

export function ServerSettingsModal({ open, onClose, server, onSaved }: ServerSettingsModalProps) {
  const [name, setName] = useState('');
  const [label, setLabel] = useState('');
  const [environment, setEnvironment] = useState<'production' | 'development' | 'test' | 'staging'>('production');
  const [unlimitedSlots, setUnlimitedSlots] = useState(true);
  const [slotLimit, setSlotLimit] = useState<number>(500);
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (server) {
      setName(server.name || '');
      setLabel(server.label || '');
      setEnvironment((server.environment as any) || 'production');
      setUnlimitedSlots(server.slot_limit === null || server.slot_limit === undefined);
      setSlotLimit(server.slot_limit || server.max_players || 500);
      setNotes(server.notes || '');
      setError(null);
    }
  }, [server]);

  if (!server) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);

    try {
      // 1. Обновление метаданных (префикс, окружение, имя, заметки)
      const metaRes = await fetch(`/api/v1/servers/${server.id}/meta`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: name.trim(),
          label: label.trim() || null,
          environment,
          notes: notes.trim() || null,
        }),
      });
      const metaData = await metaRes.json();
      if (!metaRes.ok || !metaData.success) {
        throw new Error(metaData.error || 'Ошибка сохранения настроек сервера');
      }

      // 2. Обновление лимита слотов
      const slotsRes = await fetch(`/api/v1/servers/${server.id}/slots`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          slotLimit: unlimitedSlots ? null : Number(slotLimit),
        }),
      });
      const slotsData = await slotsRes.json();
      if (!slotsRes.ok || !slotsData.success) {
        throw new Error(slotsData.error || 'Ошибка обновления слотов');
      }

      onSaved();
      onClose();
    } catch (err: any) {
      setError(err.message || 'Произошла ошибка при сохранении');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Настройка сервера & слотов"
      description={`Управление префиксами и лимитом онлайна для сервера #${server.id}`}
    >
      <form onSubmit={handleSubmit} className="space-y-5">
        {error && (
          <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-300">
            {error}
          </div>
        )}

        {/* Название сервера */}
        <div>
          <FieldLabel>Название сервера</FieldLabel>
          <input
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Основной сервер RP"
            className="field h-11 px-4 text-sm"
          />
        </div>

        {/* Визуальный префикс / Метка */}
        <div>
          <FieldLabel>
            <span className="flex items-center gap-1.5">
              <Tag className="h-3.5 w-3.5 text-brand" />
              Префикс / Визуальная метка в ЛК
            </span>
          </FieldLabel>
          <input
            value={label}
            onChange={(e) => setLabel(e.target.value.toUpperCase())}
            placeholder="DEV, TEST, PROD-1, EVENT"
            maxLength={32}
            className="field h-11 px-4 font-mono text-sm uppercase tracking-wider"
          />
          <div className="mt-2 flex flex-wrap gap-1.5">
            <span className="text-[11px] text-slate-500 self-center mr-1">Быстрый выбор:</span>
            {PRESET_PREFIXES.map((p) => (
              <button
                type="button"
                key={p}
                onClick={() => setLabel(p)}
                className={`rounded-lg border px-2 py-0.5 font-mono text-[10px] font-bold transition ${
                  label === p
                    ? 'border-brand bg-brand/20 text-brand'
                    : 'border-white/10 bg-white/5 text-slate-400 hover:border-white/30 hover:text-white'
                }`}
              >
                {p}
              </button>
            ))}
          </div>
        </div>

        {/* Тип окружения */}
        <div>
          <FieldLabel>Тип окружения (Environment)</FieldLabel>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {ENVIRONMENTS.map((env) => {
              const selected = environment === env.id;
              return (
                <button
                  type="button"
                  key={env.id}
                  onClick={() => setEnvironment(env.id as any)}
                  className={`rounded-xl border p-2.5 text-center transition-all ${
                    selected ? `${env.color} ring-1 ring-white/20` : 'border-white/10 bg-white/[0.02] text-slate-400 hover:bg-white/[0.05]'
                  }`}
                >
                  <div className="font-mono text-xs font-bold">[{env.badge}]</div>
                  <div className="mt-0.5 text-[10px] truncate">{env.label}</div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Управление лимитом слотов */}
        <div className="rounded-2xl border border-white/10 bg-ink-950/60 p-4">
          <div className="flex items-center justify-between">
            <div>
              <span className="text-xs font-bold text-white">Режим лимита онлайна</span>
              <p className="text-[11px] text-slate-400 mt-0.5">
                {unlimitedSlots
                  ? 'Без искусственного ограничения — сервер примет до 5000+ игроков'
                  : `Фиксированный лимит: не более ${slotLimit} одновременно`}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setUnlimitedSlots(!unlimitedSlots)}
              className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                unlimitedSlots ? 'bg-emeraldx' : 'bg-slate-700'
              }`}
            >
              <span
                className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                  unlimitedSlots ? 'translate-x-6' : 'translate-x-1'
                }`}
              />
            </button>
          </div>

          {!unlimitedSlots && (
            <div className="mt-4 border-t border-white/10 pt-4">
              <FieldLabel>Максимум слотов для этого сервера</FieldLabel>
              <div className="flex items-center gap-3">
                <input
                  type="number"
                  min={1}
                  max={10000}
                  value={slotLimit}
                  onChange={(e) => setSlotLimit(Math.max(1, Number(e.target.value)))}
                  className="field h-11 w-32 px-4 font-mono text-base font-bold text-brand"
                />
                <div className="flex flex-wrap gap-1.5">
                  {[32, 64, 128, 500, 1500, 5000].map((num) => (
                    <button
                      type="button"
                      key={num}
                      onClick={() => setSlotLimit(num)}
                      className={`rounded-lg border px-2.5 py-1 font-mono text-xs font-bold transition ${
                        slotLimit === num
                          ? 'border-brand bg-brand/20 text-brand'
                          : 'border-white/10 bg-white/5 text-slate-400 hover:border-white/30 hover:text-white'
                      }`}
                    >
                      {num}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Заметки */}
        <div>
          <FieldLabel>Заметки по серверу (только для вас)</FieldLabel>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Назначение, конфигурация порта, тестовые ветки..."
            rows={2}
            className="field w-full p-3 text-xs"
          />
        </div>

        {/* Кнопки */}
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
            disabled={saving}
            className="btn btn-primary h-10 px-5 text-xs font-bold disabled:opacity-50"
          >
            {saving ? <Spinner className="h-4 w-4" /> : <Check className="h-4 w-4 mr-1.5" />}
            Сохранить настройки
          </button>
        </div>
      </form>
    </Modal>
  );
}
