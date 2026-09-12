import React, { useState } from 'react';
import { Box, Check, Copy, Cpu, Download, Play, Plus, Radio, RefreshCw, Server, Settings2, ShieldCheck, Sliders, Square, Tag } from 'lucide-react';
import { Spinner, Badge } from '@/components/ui';
import { useDashboard, planTone } from './_ctx';
import { ServerSettingsModal } from './ServerSettingsModal';
import { NewServerModal } from './NewServerModal';
import { DownloadMultiplayerModal } from './DownloadMultiplayerModal';

export function ProjectsTab() {
  const {
    projects,
    selectedProject,
    servers,
    loadingServers,
    dispatchingAction,
    copied,
    resources,
    loadingResources,
    resourceActionLoading,
    setNewProjOpen,
    handleSelectProject,
    openProjectSettings,
    loadResources,
    handleResourceControl,
    handleDispatchCommand,
    copy,
    D,
  } = useDashboard();

  const [serverSettingsOpen, setServerSettingsOpen] = useState(false);
  const [serverToConfigure, setServerToConfigure] = useState<any>(null);
  const [newServerOpen, setNewServerOpen] = useState(false);
  const [downloadModalOpen, setDownloadModalOpen] = useState(false);
  const [downloadingFlv, setDownloadingFlv] = useState(false);

  const handleDownloadFlv = async (proj: any) => {
    if (!proj) return;
    try {
      setDownloadingFlv(true);
      const res = await fetch(`/api/v1/projects/${proj.id}/license-flv`);
      if (!res.ok) throw new Error('Ошибка скачивания файла лицензии');
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
      alert(e.message || 'Ошибка скачивания license.flv');
    } finally {
      setDownloadingFlv(false);
    }
  };

  return (
        <div className="relative space-y-8 animate-fade-in">
          {/* Project Switcher Bar */}
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Server className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">{D.proj.title}</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                {D.proj.hierarchy}
              </p>
            </div>
            <button
              onClick={() => setNewProjOpen(true)}
              className="btn btn-primary h-10 px-4 text-xs"
            >
              <Plus className="h-4 w-4" />
              {D.proj.create}
            </button>
          </div>

          {/* Projects Selector Pills */}
          <div className="flex flex-wrap gap-3">
            {projects.map((p) => {
              const active = selectedProject?.id === p.id;
              return (
                <button
                  key={p.id}
                  onClick={() => handleSelectProject(p)}
                  className={`flex items-center gap-3 rounded-2xl border p-4 text-left transition-all ${
                    active
                      ? 'border-brand bg-brand/10 shadow-neon-pink'
                      : 'border-white/10 bg-white/[0.02] hover:border-white/20'
                  }`}
                >
                  <div
                    className={`flex h-10 w-10 items-center justify-center rounded-xl font-mono text-sm font-black uppercase ${
                      active ? 'bg-brand text-white' : 'bg-white/5 text-slate-400'
                    }`}
                  >
                    {p.name.slice(0, 2)}
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-bold text-white">{p.name}</span>
                      <Badge tone={planTone(p.plan)}>{p.plan}</Badge>
                    </div>
                    <div className="mt-0.5 font-mono text-[11px] text-slate-400">
                      /{p.slug} · {p.max_players} {D.proj.slots}
                    </div>
                  </div>
                </button>
              );
            })}
          </div>

          {/* Selected Project Overview Card */}
          {selectedProject && (
            <div className="glass card-edge rounded-3xl p-6 sm:p-8">
              <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <span className="eyebrow text-brand">{D.proj.activeProject}</span>
                  <h3 className="mt-1 text-2xl font-black text-white">{selectedProject.name}</h3>
                  <p className="mt-1 font-mono text-xs text-slate-400">
                    ID: #{selectedProject.id} · {D.proj.plan}: <span className="text-white uppercase">{selectedProject.plan}</span> · {D.proj.limit}: <span className="text-brand">{selectedProject.max_players} {D.proj.slots}</span>
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-3">
                  <div className="rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2 font-mono text-xs text-slate-300">
                    <span className="text-slate-500 mr-2">License:</span>
                    <strong className="text-white">{selectedProject.license_key}</strong>
                  </div>
                  <button
                    onClick={() => copy(selectedProject.license_key)}
                    className="btn btn-ghost h-9 w-9 p-0"
                    title={D.proj.copyLicenseKey}
                  >
                    {copied === selectedProject.license_key ? (
                      <Check className="h-4 w-4 text-emeraldx" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </button>

                  <button
                    onClick={() => handleDownloadFlv(selectedProject)}
                    disabled={downloadingFlv}
                    className="btn h-9 border border-cyber/40 bg-cyber/10 px-3 text-xs font-semibold text-cyber transition hover:bg-cyber/20 flex items-center gap-1.5"
                    title="Скачать криптографически подписанный файл license.flv для сервера"
                  >
                    {downloadingFlv ? <Spinner className="h-3.5 w-3.5" /> : <Download className="h-3.5 w-3.5" />}
                    license.flv
                  </button>

                  <button
                    onClick={() => setDownloadModalOpen(true)}
                    className="btn btn-primary h-9 px-3.5 text-xs font-bold flex items-center gap-1.5 shadow-neon-pink"
                    title="Получить дистрибутив и варианты авто-установки (в 1 команду или готовый архив)"
                  >
                    <Download className="h-3.5 w-3.5" />
                    Файлы мультиплеера
                  </button>

                  <div className="rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2 font-mono text-xs text-slate-300">
                    <span className="text-slate-500 mr-2">Agent API Key:</span>
                    <strong className="text-brand">{selectedProject.api_key.slice(0, 16)}…</strong>
                  </div>
                  <button
                    onClick={() => copy(selectedProject.api_key)}
                    className="btn btn-ghost h-9 w-9 p-0"
                    title={D.proj.copyAgentKey}
                  >
                    {copied === selectedProject.api_key ? (
                      <Check className="h-4 w-4 text-emeraldx" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </button>

                  <button
                    onClick={() => openProjectSettings(selectedProject)}
                    className="btn h-9 border border-brand/40 bg-brand/10 px-3 text-xs font-semibold text-brand transition hover:bg-brand/20"
                    title={D.proj.settingsWebhook}
                  >
                    <Sliders className="h-4 w-4" />
                    {D.proj.settingsWebhook}
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Environments & Servers Section */}
          <div>
            <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h3 className="flex items-center gap-2 text-base font-bold text-white">
                  <Cpu className="h-5 w-5 text-cyber" />
                  {D.proj.envTitle}
                </h3>
                <p className="mt-0.5 text-xs text-slate-400">
                  {D.proj.envSub}
                </p>
              </div>
              <div className="flex items-center gap-3">
                <span className="font-mono text-xs text-slate-500">{D.proj.serversCount}: {servers.length}</span>
                {selectedProject && (
                  <button
                    onClick={() => setNewServerOpen(true)}
                    className="btn btn-primary h-8 px-3 text-xs font-semibold flex items-center gap-1.5 shadow-neon-pink"
                  >
                    <Plus className="h-3.5 w-3.5" />
                    Добавить сервер
                  </button>
                )}
              </div>
            </div>

            {loadingServers ? (
              <div className="flex justify-center p-12 text-brand">
                <Spinner className="h-6 w-6" />
              </div>
            ) : servers.length === 0 ? (
              <div className="glass card-edge rounded-3xl p-10 text-center text-xs text-slate-400">
                <Server className="mx-auto h-8 w-8 text-slate-600 mb-2" />
                {D.proj.noServers}
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
                {servers.map((srv) => {
                  const isProd = srv.environment === 'production';
                  const isDev = srv.environment === 'development';
                  const isRestarting = dispatchingAction === `${srv.id}_restart`;
                  const isStopping = dispatchingAction === `${srv.id}_stop`;
                  const isBroadcasting = dispatchingAction === `${srv.id}_broadcast`;

                  return (
                    <div
                      key={srv.id}
                      className="glass-panel card-edge flex flex-col justify-between rounded-3xl p-6 shadow-glass"
                    >
                      <div>
                        <div className="flex items-center justify-between">
                          <div className="flex flex-wrap items-center gap-1.5">
                            {srv.label && (
                              <span className="inline-flex items-center rounded-md border border-brand/50 bg-brand/15 px-2 py-0.5 font-mono text-[10px] font-bold text-brand uppercase tracking-wider">
                                [{srv.label}]
                              </span>
                            )}
                            <span
                              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase ${
                                isProd
                                  ? 'border-emeraldx/30 bg-emeraldx/15 text-emeraldx'
                                  : isDev
                                  ? 'border-cyber/30 bg-cyber/15 text-cyber'
                                  : srv.environment === 'test'
                                  ? 'border-amber-500/30 bg-amber-500/15 text-amber-300'
                                  : 'border-violetx/30 bg-violetx/15 text-violetx'
                              }`}
                            >
                              <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                              {srv.environment}
                            </span>
                          </div>
                          <span className="flex items-center gap-1 font-mono text-[10px] text-slate-400">
                            <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                            {D.proj.agentActive}
                          </span>
                        </div>

                        <h4 className="mt-3 text-base font-bold text-white">{srv.name}</h4>
                        <div className="mt-1 font-mono text-xs text-brand">
                          {srv.ip}:{srv.port}
                        </div>

                        <div className="mt-4 grid grid-cols-2 gap-2 rounded-xl border border-white/5 bg-ink-950/50 p-3 font-mono text-[11px]">
                          <div>
                            <span className="text-slate-500">{D.proj.slotsShort}:</span>{' '}
                            {srv.slot_limit ? (
                              <strong className="text-amber-300" title="Установлен лимит слотов">{srv.slot_limit} (лимит)</strong>
                            ) : (
                              <strong className="text-emeraldx" title="Безлимит">{srv.max_players} (безлимит)</strong>
                            )}
                          </div>
                          <div>
                            <span className="text-slate-500">{D.proj.protocol}:</span>{' '}
                            <strong className="text-cyber">UDP 7788</strong>
                          </div>
                          <div>
                            <span className="text-slate-500">{D.proj.agent}:</span>{' '}
                            <strong className="text-emeraldx">v1.0.4</strong>
                          </div>
                          <div>
                            <span className="text-slate-500">FastDL:</span>{' '}
                            <strong className="text-white">CDN Active</strong>
                          </div>
                        </div>
                      </div>

                      {/* Server Controls & Configuration */}
                      <div className="mt-5 space-y-2 border-t border-white/[0.08] pt-4">
                        <button
                          onClick={() => {
                            setServerToConfigure(srv);
                            setServerSettingsOpen(true);
                          }}
                          className="btn btn-ghost h-8 w-full border border-white/10 text-[11px] font-semibold text-slate-300 hover:border-brand/40 hover:text-white flex items-center justify-center gap-1.5 transition"
                        >
                          <Settings2 className="h-3.5 w-3.5 text-brand" />
                          Префикс & Лимит слотов
                        </button>
                        <div className="grid grid-cols-2 gap-2">
                          <button
                            onClick={() => handleDispatchCommand(srv.id, 'restart')}
                            disabled={isRestarting}
                            className="btn h-9 border border-amber-500/30 bg-amber-500/10 text-xs font-semibold text-amber-300 transition hover:bg-amber-500/20 disabled:opacity-50"
                          >
                            {isRestarting ? <Spinner className="h-3.5 w-3.5" /> : <RefreshCw className="h-3.5 w-3.5" />}
                            {D.proj.restart}
                          </button>
                          <button
                            onClick={() => handleDispatchCommand(srv.id, 'stop')}
                            disabled={isStopping}
                            className="btn h-9 border border-red-500/30 bg-red-500/10 text-xs font-semibold text-red-400 transition hover:bg-red-500/20 disabled:opacity-50"
                          >
                            {isStopping ? <Spinner className="h-3.5 w-3.5" /> : <Square className="h-3.5 w-3.5" />}
                            {D.proj.stop}
                          </button>
                        </div>
                        <button
                          onClick={() => handleDispatchCommand(srv.id, 'broadcast')}
                          disabled={isBroadcasting}
                          className="btn btn-ghost h-9 w-full text-xs font-semibold text-slate-300"
                        >
                          {isBroadcasting ? <Spinner className="h-3.5 w-3.5" /> : <Radio className="h-3.5 w-3.5 text-brand" />}
                          {D.proj.announce}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Resource Manager Section */}
            {selectedProject && servers.length > 0 && (
              <div className="mt-8 glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-7">
                <div className="mb-5 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <h4 className="flex items-center gap-2 text-base font-bold text-white">
                      <Box className="h-5 w-5 text-brand" />
                      {D.proj.resTitle}
                    </h4>
                    <p className="text-xs text-slate-400">
                      {D.proj.resSub}
                    </p>
                  </div>
                  <button
                    onClick={() => loadResources(servers[0].id)}
                    disabled={loadingResources}
                    className="btn btn-ghost h-9 px-3 text-xs"
                  >
                    <RefreshCw className={`h-3.5 w-3.5 ${loadingResources ? 'animate-spin text-brand' : ''}`} />
                    {D.proj.resReload}
                  </button>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full min-w-[600px] text-left text-xs">
                    <thead>
                      <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                        <th className="pb-3 pr-3 font-semibold">{D.proj.thResource}</th>
                        <th className="pb-3 pr-3 font-semibold">{D.proj.thType}</th>
                        <th className="pb-3 pr-3 font-semibold">{D.proj.thVersion}</th>
                        <th className="pb-3 pr-3 font-semibold">{D.proj.thStatus}</th>
                        <th className="pb-3 pr-3 text-right font-semibold">{D.proj.thControl}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-white/[0.05] text-slate-300 font-mono">
                      {resources.map((res) => {
                        const isRunning = res.status === 'running';
                        const isStarting = resourceActionLoading === `${res.name}_start`;
                        const isStopping = resourceActionLoading === `${res.name}_stop`;
                        const isRestarting = resourceActionLoading === `${res.name}_restart`;

                        return (
                          <tr key={res.name} className="transition-colors hover:bg-white/[0.02]">
                            <td className="py-3 pr-3 font-bold text-white">
                              <span className="flex items-center gap-2">
                                <Box className="h-3.5 w-3.5 text-slate-400" />
                                {res.name}
                              </span>
                            </td>
                            <td className="py-3 pr-3">
                              <span className="rounded bg-white/5 px-2 py-0.5 text-[10px] text-cyber uppercase font-bold">
                                {res.type}
                              </span>
                            </td>
                            <td className="py-3 pr-3 text-slate-400">{res.version}</td>
                            <td className="py-3 pr-3">
                              <span
                                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[10px] font-bold ${
                                  isRunning
                                    ? 'bg-emeraldx/15 text-emeraldx border border-emeraldx/30'
                                    : 'bg-slate-700/20 text-slate-400 border border-slate-600/30'
                                }`}
                              >
                                <span className={`h-1.5 w-1.5 rounded-full ${isRunning ? 'bg-emeraldx animate-pulse' : 'bg-slate-500'}`} />
                                {isRunning ? D.proj.running : D.proj.stopped}
                              </span>
                            </td>
                            <td className="py-3 pr-3 text-right">
                              <div className="flex items-center justify-end gap-1.5">
                                {isRunning ? (
                                  <>
                                    <button
                                      onClick={() => handleResourceControl(servers[0].id, res.name, 'restart')}
                                      disabled={isRestarting}
                                      className="btn h-8 border border-white/10 bg-white/5 px-2.5 text-[11px] text-slate-200 transition hover:bg-white/10"
                                      title={D.proj.resRestart}
                                    >
                                      {isRestarting ? <Spinner className="h-3 w-3" /> : <RefreshCw className="h-3 w-3" />}
                                    </button>
                                    <button
                                      onClick={() => handleResourceControl(servers[0].id, res.name, 'stop')}
                                      disabled={isStopping}
                                      className="btn h-8 border border-red-500/30 bg-red-500/10 px-2.5 text-[11px] text-red-400 transition hover:bg-red-500/20"
                                      title={D.proj.resStop}
                                    >
                                      {isStopping ? <Spinner className="h-3 w-3" /> : <Square className="h-3 w-3" />}
                                    </button>
                                  </>
                                ) : (
                                  <button
                                    onClick={() => handleResourceControl(servers[0].id, res.name, 'start')}
                                    disabled={isStarting}
                                    className="btn h-8 border border-emeraldx/30 bg-emeraldx/10 px-2.5 text-[11px] text-emeraldx transition hover:bg-emeraldx/20"
                                    title={D.proj.resStart}
                                  >
                                    {isStarting ? <Spinner className="h-3 w-3" /> : <Play className="h-3 w-3" />}
                                  </button>
                                )}
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>

          {/* Server Configuration & Prefixes Modal */}
          <ServerSettingsModal
            open={serverSettingsOpen}
            onClose={() => {
              setServerSettingsOpen(false);
              setServerToConfigure(null);
            }}
            server={serverToConfigure}
            onSaved={() => {
              if (selectedProject) void handleSelectProject(selectedProject);
            }}
          />

          {/* New Server Instance Modal */}
          {selectedProject && (
            <NewServerModal
              open={newServerOpen}
              onClose={() => setNewServerOpen(false)}
              projectId={selectedProject.id}
              onCreated={() => {
                if (selectedProject) void handleSelectProject(selectedProject);
              }}
            />
          )}

          {/* Download Multiplayer / 3 Distribution Options Modal */}
          <DownloadMultiplayerModal
            open={downloadModalOpen}
            onClose={() => setDownloadModalOpen(false)}
            project={selectedProject}
          />
        </div>
  );
}
