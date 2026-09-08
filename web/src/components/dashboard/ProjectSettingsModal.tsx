'use client';

import React from 'react';
import { Bell } from 'lucide-react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function ProjectSettingsModal() {
  const {
    settingsModalOpen,
    setSettingsModalOpen,
    settingHwid,
    setSettingHwid,
    settingVpn,
    setSettingVpn,
    settingMaxAccs,
    setSettingMaxAccs,
    settingDiscord,
    setSettingDiscord,
    settingTgToken,
    setSettingTgToken,
    settingTgChat,
    setSettingTgChat,
    settingAlertsEnabled,
    setSettingAlertsEnabled,
    savingSettings,
    testingWebhook,
    saveProjectSettings,
    testWebhooks,
    D,
  } = useDashboard();
  return (
      <Modal
        open={settingsModalOpen}
        onClose={() => setSettingsModalOpen(false)}
        title={D.modal.settingsTitle}
        description={D.modal.settingsDesc}
        maxWidth="max-w-2xl"
      >
        <form onSubmit={saveProjectSettings} className="space-y-6">
          {/* HWID Policy Selection */}
          <div>
            <FieldLabel>{D.modal.hwidPolicy}</FieldLabel>
            <div className="grid grid-cols-3 gap-2.5">
              {[
                {
                  id: 'strict',
                  title: D.modal.hwidStrict,
                  desc: D.modal.hwidStrictDesc,
                  tone: 'red',
                },
                {
                  id: 'lenient',
                  title: D.modal.hwidLenient,
                  desc: D.modal.hwidLenientDesc,
                  tone: 'brand',
                },
                {
                  id: 'disabled',
                  title: D.modal.hwidDisabled,
                  desc: D.modal.hwidDisabledDesc,
                  tone: 'slate',
                },
              ].map((m) => (
                <button
                  type="button"
                  key={m.id}
                  onClick={() => setSettingHwid(m.id as any)}
                  className={`rounded-2xl border p-3.5 text-left transition ${
                    settingHwid === m.id
                      ? m.tone === 'brand'
                        ? 'border-brand/60 bg-brand/10 shadow-neon-pink'
                        : m.tone === 'red'
                        ? 'border-red-500/60 bg-red-500/10'
                        : 'border-white/30 bg-white/10'
                      : 'border-white/10 bg-white/[0.02]'
                  }`}
                >
                  <div className="text-xs font-bold text-white">{m.title}</div>
                  <div className="mt-1 text-[10px] text-slate-400">{m.desc}</div>
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <FieldLabel>{D.modal.maxAccs}</FieldLabel>
              <input
                type="number"
                min={1}
                max={10}
                value={settingMaxAccs}
                onChange={(e) => setSettingMaxAccs(Number(e.target.value))}
                className="field h-11 px-4 font-mono"
              />
            </div>
            <div className="flex flex-col justify-end">
              <label className="flex items-center gap-3 cursor-pointer pb-2.5">
                <input
                  type="checkbox"
                  checked={settingVpn}
                  onChange={(e) => setSettingVpn(e.target.checked)}
                  className="h-4 w-4 rounded accent-brand"
                />
                <span className="text-xs text-slate-200 font-semibold">
                  {D.modal.allowVpn}
                </span>
              </label>
            </div>
          </div>

          {/* Webhooks Section */}
          <div className="space-y-4 border-t border-white/[0.08] pt-5">
            <div className="flex items-center justify-between">
              <div>
                <h4 className="flex items-center gap-2 text-sm font-bold text-white">
                  <Bell className="h-4 w-4 text-brand" />
                  {D.modal.alertsTitle}
                </h4>
                <p className="text-[11px] text-slate-400">
                  {D.modal.alertsSub}
                </p>
              </div>
              <label className="flex items-center gap-2 text-xs text-slate-300 font-semibold cursor-pointer">
                <input
                  type="checkbox"
                  checked={settingAlertsEnabled}
                  onChange={(e) => setSettingAlertsEnabled(e.target.checked)}
                  className="h-4 w-4 rounded accent-brand"
                />
                {D.modal.enabled}
              </label>
            </div>

            <div>
              <FieldLabel>Discord Webhook URL</FieldLabel>
              <div className="flex gap-2">
                <input
                  value={settingDiscord}
                  onChange={(e) => setSettingDiscord(e.target.value)}
                  placeholder="https://discord.com/api/webhooks/..."
                  className="field h-10 flex-1 px-3 font-mono text-xs"
                />
                <button
                  type="button"
                  onClick={() => testWebhooks('discord')}
                  disabled={testingWebhook || !settingDiscord}
                  className="btn btn-ghost h-10 px-3 text-xs text-brand disabled:opacity-40"
                >
                  {D.modal.test}
                </button>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <FieldLabel>Telegram Bot Token</FieldLabel>
                <input
                  value={settingTgToken}
                  onChange={(e) => setSettingTgToken(e.target.value)}
                  placeholder="123456789:ABCdefGHIjklMNO..."
                  className="field h-10 px-3 font-mono text-xs"
                />
              </div>
              <div>
                <FieldLabel>Telegram Chat ID</FieldLabel>
                <div className="flex gap-2">
                  <input
                    value={settingTgChat}
                    onChange={(e) => setSettingTgChat(e.target.value)}
                    placeholder="-1001234567890"
                    className="field h-10 flex-1 px-3 font-mono text-xs"
                  />
                  <button
                    type="button"
                    onClick={() => testWebhooks('telegram')}
                    disabled={testingWebhook || !settingTgToken || !settingTgChat}
                    className="btn btn-ghost h-10 px-3 text-xs text-cyber disabled:opacity-40"
                  >
                    {D.modal.test}
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button
              type="button"
              onClick={() => setSettingsModalOpen(false)}
              className="px-4 py-2 text-xs text-slate-400 transition hover:text-white"
            >
              {D.cancel}
            </button>
            <button
              type="submit"
              disabled={savingSettings}
              className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50"
            >
              {savingSettings ? <Spinner className="h-4 w-4" /> : null}
              {D.modal.saveProjectParams}
            </button>
          </div>
        </form>
      </Modal>
  );
}
