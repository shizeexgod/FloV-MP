'use client';

import React, { useState } from 'react';
import { Modal, FieldLabel, Spinner } from '@/components/ui';
import { Archive, Terminal, FileCode, Copy, Check, Download, Upload, Server, ShieldCheck, Sparkles } from 'lucide-react';
import type { Project } from './_ctx';

interface DownloadMultiplayerModalProps {
  open: boolean;
  onClose: () => void;
  project: Project | null;
}

export function DownloadMultiplayerModal({ open, onClose, project }: DownloadMultiplayerModalProps) {
  const [activeTab, setActiveTab] = useState<'archive' | 'oneline' | 'manual'>('archive');
  const [copiedCmd, setCopiedCmd] = useState(false);
  const [copiedKey, setCopiedKey] = useState(false);
  const [manualKey, setManualKey] = useState(project?.license_key || '');
  const [attachedFile, setAttachedFile] = useState<File | null>(null);
  const [downloading, setDownloading] = useState(false);

  const licenseKey = project?.license_key || manualKey || 'FLV-DEMO-XXXX-XXXX';
  const installCommand = `curl -sSL https://flov-mp.ru/install.sh | bash -s -- --key ${licenseKey}`;

  const copyCommand = () => {
    navigator.clipboard.writeText(installCommand);
    setCopiedCmd(true);
    setTimeout(() => setCopiedCmd(false), 2000);
  };

  const copyKey = () => {
    navigator.clipboard.writeText(licenseKey);
    setCopiedKey(true);
    setTimeout(() => setCopiedKey(false), 2000);
  };

  const handleDownloadPreConfigured = async () => {
    if (!project) return;
    setDownloading(true);
    try {
      // 1. Скачиваем официальный подписанный license.flv
      const res = await fetch(`/api/v1/projects/${project.id}/license-flv`);
      if (!res.ok) throw new Error('Ошибка генерации лицензии');
      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'license.flv';
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (e: any) {
      alert(e.message || 'Ошибка скачивания');
    } finally {
      setDownloading(false);
    }
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setAttachedFile(e.target.files[0]);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Получить файлы мультиплеера FloV:MP"
      description="Выберите удобный вариант установки и автоматической активации игрового сервера"
    >
      <div className="space-y-6">
        {/* Вкладки 3 вариантов */}
        <div className="grid grid-cols-3 gap-2 rounded-2xl border border-white/10 bg-ink-950/70 p-1.5">
          <button
            type="button"
            onClick={() => setActiveTab('archive')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-all ${
              activeTab === 'archive'
                ? 'bg-brand text-white shadow-neon-pink'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Archive className="h-3.5 w-3.5" />
            <span>Архив с лицензией</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('oneline')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-all ${
              activeTab === 'oneline'
                ? 'bg-cyber text-ink-950 font-black shadow-neon-cyan'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Terminal className="h-3.5 w-3.5" />
            <span>В 1 команду (Curl)</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('manual')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-all ${
              activeTab === 'manual'
                ? 'bg-white/20 text-white'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <FileCode className="h-3.5 w-3.5" />
            <span>Ключ / Файл</span>
          </button>
        </div>

        {/* ВАРИАНТ 1: Готовый архив с лицензией внутри */}
        {activeTab === 'archive' && (
          <div className="space-y-4 rounded-2xl border border-white/10 bg-white/[0.02] p-5">
            <div className="flex items-start gap-3">
              <div className="rounded-xl border border-brand/40 bg-brand/10 p-2 text-brand">
                <Sparkles className="h-5 w-5" />
              </div>
              <div>
                <h4 className="text-sm font-bold text-white">Пакет с авто-активацией</h4>
                <p className="text-xs text-slate-400 mt-0.5 leading-relaxed">
                  Файлы сервера, куда уже автоматически вшита ваша лицензия <span className="font-mono text-brand font-bold">{licenseKey}</span>. Достаточно распаковать на сервере.
                </p>
              </div>
            </div>

            <div className="rounded-xl border border-white/5 bg-ink-950/60 p-3.5 font-mono text-xs text-slate-300 space-y-1.5">
              <div className="flex justify-between">
                <span className="text-slate-500">Проект:</span>
                <span className="text-white font-bold">{project?.name || 'Мой RP Сервер'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">План:</span>
                <span className="text-cyber uppercase font-bold">{project?.plan || 'Enterprise'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Лимит слотов:</span>
                <span className="text-emeraldx font-bold">{project?.max_players || 5000} слотов</span>
              </div>
            </div>

            <button
              onClick={handleDownloadPreConfigured}
              disabled={downloading}
              className="btn btn-primary h-11 w-full text-xs font-bold flex items-center justify-center gap-2 shadow-neon-pink"
            >
              {downloading ? <Spinner className="h-4 w-4" /> : <Download className="h-4 w-4" />}
              Скачать официальный license.flv
            </button>
            <p className="text-[11px] text-slate-500 text-center">
              Поместите скачанный файл license.flv в корневую папку сервера рядом с flovmp-server
            </p>
          </div>
        )}

        {/* ВАРИАНТ 2: Установка в 1 команду */}
        {activeTab === 'oneline' && (
          <div className="space-y-4 rounded-2xl border border-cyber/30 bg-cyber/[0.03] p-5">
            <div className="flex items-start gap-3">
              <div className="rounded-xl border border-cyber/40 bg-cyber/10 p-2 text-cyber">
                <Terminal className="h-5 w-5" />
              </div>
              <div>
                <h4 className="text-sm font-bold text-white">Автоматическая установка в 1 строку</h4>
                <p className="text-xs text-slate-400 mt-0.5 leading-relaxed">
                  Выполните команду на чистом сервере Ubuntu 22.04 / 24.04. Скрипт сам установит зависимости, подтянет лицензию, настроит MariaDB и голосовой сервер с авто-перезапуском.
                </p>
              </div>
            </div>

            <div className="relative rounded-xl border border-cyber/30 bg-ink-950 p-3.5 font-mono text-xs text-cyber break-all">
              {installCommand}
              <button
                onClick={copyCommand}
                className="absolute right-2 top-2 rounded-lg border border-white/10 bg-white/5 p-1.5 text-slate-300 hover:text-white transition"
                title="Скопировать команду"
              >
                {copiedCmd ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
              </button>
            </div>

            <div className="grid grid-cols-2 gap-2 text-[11px] text-slate-400">
              <div className="flex items-center gap-1.5">
                <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                Авто-настройка MariaDB
              </div>
              <div className="flex items-center gap-1.5">
                <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                Голосовой сервер (PartOf=)
              </div>
              <div className="flex items-center gap-1.5">
                <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                .NET 8 CoreCLR runtime
              </div>
              <div className="flex items-center gap-1.5">
                <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                Лицензия вшивается сама
              </div>
            </div>
          </div>
        )}

        {/* ВАРИАНТ 3: Поле ключа или прикрепление файла */}
        {activeTab === 'manual' && (
          <div className="space-y-4 rounded-2xl border border-white/10 bg-white/[0.02] p-5">
            <div>
              <FieldLabel>Ключ лицензии (FLV-XXXX-XXXX-XXXX)</FieldLabel>
              <div className="flex gap-2">
                <input
                  value={manualKey}
                  onChange={(e) => setManualKey(e.target.value.toUpperCase())}
                  placeholder="FLV-XXXX-XXXX-XXXX"
                  className="field h-11 px-4 font-mono text-sm tracking-wider uppercase text-brand"
                />
                <button
                  type="button"
                  onClick={copyKey}
                  className="btn btn-ghost h-11 px-3 border border-white/10 text-slate-300 hover:text-white"
                  title="Скопировать ключ"
                >
                  {copiedKey ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <div className="relative flex py-1 items-center">
              <div className="flex-grow border-t border-white/10" />
              <span className="flex-shrink mx-3 text-[11px] text-slate-500 uppercase font-mono">или прикрепите файл</span>
              <div className="flex-grow border-t border-white/10" />
            </div>

            <div>
              <FieldLabel>Файл лицензии (license.flv)</FieldLabel>
              <label className="flex flex-col items-center justify-center rounded-xl border border-dashed border-white/20 bg-ink-950/50 p-4 text-center cursor-pointer hover:border-brand/50 transition">
                <Upload className="h-6 w-6 text-slate-400 mb-1" />
                <span className="text-xs text-slate-300 font-semibold">
                  {attachedFile ? attachedFile.name : 'Нажмите для выбора license.flv'}
                </span>
                <span className="text-[10px] text-slate-500 mt-0.5">RSA-2048 криптографический файл</span>
                <input
                  type="file"
                  accept=".flv,.json"
                  onChange={handleFileUpload}
                  className="hidden"
                />
              </label>
            </div>

            {attachedFile && (
              <div className="rounded-xl border border-emeraldx/30 bg-emeraldx/10 p-3 text-xs text-emeraldx flex items-center justify-between">
                <span>Файл {attachedFile.name} готов к установке</span>
                <Check className="h-4 w-4" />
              </div>
            )}
          </div>
        )}

        {/* Футер */}
        <div className="flex justify-end border-t border-white/[0.08] pt-4">
          <button
            type="button"
            onClick={onClose}
            className="btn btn-ghost h-9 px-4 text-xs text-slate-300 hover:text-white"
          >
            Закрыть
          </button>
        </div>
      </div>
    </Modal>
  );
}
