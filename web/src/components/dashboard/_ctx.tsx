'use client';

import React, { createContext, useContext } from 'react';

/* ----------------------------- shared types ----------------------------- */
export interface License {
  id: number;
  license_key: string;
  server_name: string;
  bound_ip: string;
  plan: string;
  max_players: number;
  is_active: number;
  expires_at: string;
  created_at: string;
  last_verified_at?: string;
}

export interface Project {
  id: number;
  user_id: number;
  name: string;
  slug: string;
  license_key: string;
  plan: string;
  max_players: number;
  api_key: string;
  hwid_policy?: 'strict' | 'lenient' | 'disabled';
  allow_vpn?: boolean;
  max_accounts_per_hwid?: number;
  discord_webhook_url?: string;
  telegram_webhook_token?: string;
  telegram_chat_id?: string;
  webhook_alerts_enabled?: boolean;
  is_active: number;
  expires_at: string;
  created_at: string;
}

export interface ResourceItem {
  id: number;
  server_id: number;
  name: string;
  type: string;
  status: 'running' | 'stopped' | 'failed';
  version: string;
  author: string;
}

export interface ServerInstance {
  id: number;
  project_id: number;
  environment: 'production' | 'development' | 'test';
  name: string;
  ip: string;
  port: number;
  agent_token: string;
  status: 'online' | 'offline' | 'restarting';
  players_count: number;
  max_players: number;
  tick_rate: number;
  memory_mb: number;
  cpu_percent: number;
  last_heartbeat?: string;
  created_at: string;
}

export interface UserProfile {
  id: number;
  username: string;
  email: string;
  role: string;
  telegram?: string;
  created_at?: string;
}

export interface Invoice {
  id: number;
  user_id: number;
  license_id?: number;
  amount_rub: number;
  plan: string;
  payment_method: string;
  payment_id?: string;
  status: 'pending' | 'paid' | 'cancelled';
  created_at: string;
  paid_at?: string;
}

export interface TelemetryPoint {
  id: number;
  license_key: string;
  players: number;
  max_players: number;
  tick_rate: number;
  memory_mb: number;
  fps: number;
  server_ip: string;
  recorded_at: string;
}

export interface LauncherBuildResult {
  buildId: number;
  projectName: string;
  primaryColor: string;
  downloadUrl: string;
  config: { serverIp: string; serverPort: number; licenseKey: string };
  message: string;
}

export type TabKey =
  | 'projects' | 'console' | 'troubleshoot' | 'overview'
  | 'telemetry' | 'sdk' | 'builder' | 'billing' | 'affiliate'
  | 'watchdog' | 'logs' | 'settings';

export interface TwoFaState {
  enabled: boolean;
  loading: boolean;
  setup: { secret: string; otpauthUri: string } | null;
  code: string;
  busy: boolean;
  err: string;
}

/* ----------------------------- shared consts / helpers ----------------------------- */
export const COLOR_PRESETS = [
  { name: 'FloV Pink', hex: '#ff3d8a' },
  { name: 'Majestic Cyan', hex: '#00f0ff' },
  { name: 'Amber Gold', hex: '#f59e0b' },
  { name: 'Emerald', hex: '#10b981' },
  { name: 'Purple', hex: '#8b5cf6' },
];

export const planTone = (plan: string) =>
  plan === 'enterprise' ? 'cyber' : plan === 'business' ? 'brand' : 'slate';

export const dateShort = (s: string) => new Date(s).toLocaleDateString('ru-RU');
export const timeShort = (s: string) => new Date(s).toLocaleTimeString('ru-RU');

/* ----------------------------- context ----------------------------- */
// The dashboard container (page.tsx) owns all state + handlers and provides them
// here. Tab / modal components are purely presentational and read from this.
export interface DashCtx {
  D: any;
  BUILD_STAGES: string[];
  primaryLic: License | undefined;
  isIpBound: boolean | undefined;
  promoCode: string;
  latest: TelemetryPoint | null;
  onboarding: { done: boolean; title: string; note: string; warn?: boolean }[];
  user: UserProfile | null;
  licenses: License[];
  tab: TabKey;
  projects: Project[];
  selectedProject: Project | null;
  servers: ServerInstance[];
  dispatchingAction: string | null;
  consoleLogs: { id: number; time: string; tag: string; text: string; tone: 'info' | 'warn' | 'error' | 'cmd' }[];
  consoleInput: string;
  troubleshootText: string;
  diagnosing: boolean;
  diagnosticResult: any;
  showKeyId: number | null;
  copied: string | null;
  ipLicense: License | null;
  ipValue: string;
  ipName: string;
  ipErr: string;
  savingIp: boolean;
  settingsModalOpen: boolean;
  settingHwid: 'strict' | 'lenient' | 'disabled';
  settingVpn: boolean;
  settingMaxAccs: number;
  settingDiscord: string;
  settingTgToken: string;
  settingTgChat: string;
  settingAlertsEnabled: boolean;
  savingSettings: boolean;
  testingWebhook: boolean;
  resources: ResourceItem[];
  loadingResources: boolean;
  loadingServers: boolean;
  resourceActionLoading: string | null;
  sseActive: boolean;
  newProjOpen: boolean;
  newProjName: string;
  newProjSlug: string;
  newProjPlan: string;
  creatingProj: boolean;
  newLicOpen: boolean;
  newPlan: string;
  newName: string;
  newIp: string;
  creatingLic: boolean;
  invoices: Invoice[];
  loadingInvoices: boolean;
  payingId: number | null;
  invoiceOpen: boolean;
  invPlan: string;
  invPeriod: 'monthly' | 'halfYear' | 'year';
  invMethod: 'card' | 'sbp' | 'crypto';
  creatingInvoice: boolean;
  telemetry: TelemetryPoint[];
  loadingTelemetry: boolean;
  sendingHb: boolean;
  bProject: string;
  bColor: string;
  bIp: string;
  bPort: string;
  building: boolean;
  buildStage: number;
  buildResult: LauncherBuildResult | null;
  twoFa: TwoFaState;
  setTwoFa: React.Dispatch<React.SetStateAction<TwoFaState>>;
  start2fa: () => void;
  confirm2fa: () => void;
  disable2fa: () => void;
  // setters + handlers — loosely typed
  [key: string]: any;
}

const DashboardContext = createContext<DashCtx | null>(null);

export function DashboardProvider({ value, children }: { value: DashCtx; children: React.ReactNode }) {
  return <DashboardContext.Provider value={value}>{children}</DashboardContext.Provider>;
}

export function useDashboard(): DashCtx {
  const ctx = useContext(DashboardContext);
  if (!ctx) throw new Error('useDashboard must be used inside <DashboardProvider>');
  return ctx;
}

/* ----------------------------- shared sub-component ----------------------------- */
export function MetricCard({
  icon: Icon,
  tone,
  ring,
  label,
  value,
  unit,
  foot,
}: {
  icon: React.ElementType;
  tone: string;
  ring: string;
  label: string;
  value: string;
  unit: string;
  foot: string;
}) {
  return (
    <div className="glass card-edge rounded-2xl p-6">
      <div className="flex items-center justify-between">
        <span className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{label}</span>
        <span className={`flex h-8 w-8 items-center justify-center rounded-lg border ${ring} ${tone}`}>
          <Icon className="h-4 w-4" />
        </span>
      </div>
      <div className="mt-3 font-mono text-3xl font-black text-white">
        {value} <span className="text-xs font-normal text-slate-500">{unit}</span>
      </div>
      <div className={`mt-2 font-mono text-[11px] ${tone}`}>{foot}</div>
    </div>
  );
}
