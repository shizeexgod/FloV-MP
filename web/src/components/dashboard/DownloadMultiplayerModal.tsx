'use client';

import React, { useState } from 'react';
import { Modal, FieldLabel, Spinner } from '@/components/ui';
import { Archive, Terminal, FileCode, Copy, Check, Download, Upload, Sparkles } from 'lucide-react';
import { useDashboard, type Project } from './_ctx';

interface DownloadMultiplayerModalProps {
  open: boolean;
  onClose: () => void;
  project: Project | null;
}

export function DownloadMultiplayerModal({ open, onClose, project }: DownloadMultiplayerModalProps) {
  const { D } = useDashboard();
  const m = D.download;
  const [activeTab, setActiveTab] = useState<'archive' | 'oneline' | 'manual'>('archive');
  const [copiedKey, setCopiedKey] = useState(false);
  const [manualKey, setManualKey] = useState(project?.license_key || '');
  const [attachedFile, setAttachedFile] = useState<File | null>(null);
  const [downloading, setDownloading] = useState(false);

  const licenseKey = project?.license_key || manualKey || '—';
  const copyKey = () => {
    navigator.clipboard.writeText(licenseKey).catch(() => {});
    setCopiedKey(true);
    setTimeout(() => setCopiedKey(false), 2000);
  };

  const handleDownloadPreConfigured = async () => {
    if (!project) return;
    setDownloading(true);
    try {
      // 1. Скачиваем официальный подписанный license.flv
      const res = await fetch(`/api/v1/projects/${project.id}/license-flv`);
      if (!res.ok) throw new Error(m.generateError);
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
      alert(e.message || m.downloadError);
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
      title={m.title}
      description={m.description}
    >
      <div className="space-y-6">
        {/* Вкладки 3 вариантов */}
        <div className="grid grid-cols-3 gap-2 rounded-2xl border border-white/10 bg-ink-950/70 p-1.5">
          <button
            type="button"
            onClick={() => setActiveTab('archive')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-[border-color,background-color,color,transform] ${
              activeTab === 'archive'
                ? 'bg-brand text-white shadow-neon-pink'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Archive className="h-3.5 w-3.5" />
            <span>{m.tabArchive}</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('oneline')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-[border-color,background-color,color,transform] ${
              activeTab === 'oneline'
                ? 'bg-cyber text-ink-950 font-black shadow-neon-cyan'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Terminal className="h-3.5 w-3.5" />
            <span>{m.tabOneLine}</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('manual')}
            className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-xs font-bold transition-[border-color,background-color,color,transform] ${
              activeTab === 'manual'
                ? 'bg-white/20 text-white'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <FileCode className="h-3.5 w-3.5" />
            <span>{m.tabManual}</span>
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
                <h4 className="text-sm font-bold text-white">{m.signedTitle}</h4>
                <p className="text-xs text-slate-400 mt-0.5 leading-relaxed">
                  {m.signedBefore} <span className="font-mono text-white">license.flv</span> {m.signedFor} <span className="font-mono text-brand font-bold">{licenseKey}</span> {m.signedAfter}
                </p>
              </div>
            </div>

            <div className="rounded-xl border border-white/5 bg-ink-950/60 p-3.5 font-mono text-xs text-slate-300 space-y-1.5">
              <div className="flex justify-between">
                <span className="text-slate-500">{m.project}:</span>
                <span className="text-white font-bold">{project?.name || m.defaultProject}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">{m.plan}:</span>
                <span className="text-cyber uppercase font-bold">{project?.plan || 'Lifetime'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">{m.slotLimit}:</span>
                <span className="text-emeraldx font-bold">{project?.max_players || '—'} {m.slots}</span>
              </div>
            </div>

            <button
              onClick={handleDownloadPreConfigured}
              disabled={downloading}
              className="btn btn-primary h-11 w-full text-xs font-bold flex items-center justify-center gap-2 shadow-neon-pink"
            >
              {downloading ? <Spinner className="h-4 w-4" /> : <Download className="h-4 w-4" />}
              {m.downloadButton}
            </button>
            <p className="text-[11px] text-slate-500 text-center">
              {m.rootHint}
            </p>
          </div>
        )}

        {/* ВАРИАНТ 2: Установка в 1 команду */}
        {activeTab === 'oneline' && (
          <div className="space-y-4 rounded-2xl border border-white/10 bg-white/[0.02] p-5">
            <div className="flex items-start gap-3">
              <div className="rounded-xl border border-white/10 bg-white/[0.04] p-2 text-white/45">
                <Terminal className="h-5 w-5" />
              </div>
              <div>
                <h4 className="text-sm font-bold text-white">{m.autoTitle}</h4>
                <p className="text-xs text-slate-400 mt-0.5 leading-relaxed">
                  {m.autoText}
                </p>
              </div>
            </div>
            <div className="rounded-xl border border-dashed border-white/10 bg-black/15 p-4 text-center text-[11px] text-white/35">
              {m.autoPlaceholder}
            </div>
          </div>
        )}

        {/* ВАРИАНТ 3: Поле ключа или прикрепление файла */}
        {activeTab === 'manual' && (
          <div className="space-y-4 rounded-2xl border border-white/10 bg-white/[0.02] p-5">
            <div>
              <FieldLabel>{m.keyLabel}</FieldLabel>
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
                  title={m.copyKey}
                  aria-label={m.copyKey}
                >
                  {copiedKey ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <div className="relative flex py-1 items-center">
              <div className="flex-grow border-t border-white/10" />
              <span className="flex-shrink mx-3 text-[11px] text-slate-500 uppercase font-mono">{m.orAttach}</span>
              <div className="flex-grow border-t border-white/10" />
            </div>

            <div>
              <FieldLabel>{m.fileLabel}</FieldLabel>
              <label className="flex flex-col items-center justify-center rounded-xl border border-dashed border-white/20 bg-ink-950/50 p-4 text-center cursor-pointer hover:border-brand/50 transition">
                <Upload className="h-6 w-6 text-slate-400 mb-1" />
                <span className="text-xs text-slate-300 font-semibold">
                  {attachedFile ? attachedFile.name : m.chooseFile}
                </span>
                <span className="text-[10px] text-slate-500 mt-0.5">{m.cryptoFile}</span>
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
                <span>{m.fileReady} {attachedFile.name}</span>
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
            {m.close}
          </button>
        </div>
      </div>
    </Modal>
  );
}
