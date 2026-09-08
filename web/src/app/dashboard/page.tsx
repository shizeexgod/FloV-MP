'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Activity, AlertTriangle, CreditCard, Download, KeyRound, Layers, Percent, Plus, ScrollText, Server, Settings, Terminal, Zap } from 'lucide-react';
import { Badge, FieldLabel, Modal, Spinner, useToast } from '@/components/ui';
import { useT } from '@/lib/i18n';

import {
  DashboardProvider,
  type License, type Project, type ResourceItem, type ServerInstance, type UserProfile,
  type Invoice, type TelemetryPoint, type LauncherBuildResult, type TabKey,
} from '@/components/dashboard/_ctx';
import { ProjectsTab } from '@/components/dashboard/ProjectsTab';
import { ConsoleTab } from '@/components/dashboard/ConsoleTab';
import { TroubleshootTab } from '@/components/dashboard/TroubleshootTab';
import { OverviewTab } from '@/components/dashboard/OverviewTab';
import { TelemetryTab } from '@/components/dashboard/TelemetryTab';
import { SdkTab } from '@/components/dashboard/SdkTab';
import { BuilderTab } from '@/components/dashboard/BuilderTab';
import { BillingTab } from '@/components/dashboard/BillingTab';
import { AffiliateTab } from '@/components/dashboard/AffiliateTab';
import { WatchdogTab } from '@/components/dashboard/WatchdogTab';
import { LogsTab } from '@/components/dashboard/LogsTab';
import { SettingsTab } from '@/components/dashboard/SettingsTab';
import { IpBindModal } from '@/components/dashboard/IpBindModal';
import { NewLicenseModal } from '@/components/dashboard/NewLicenseModal';
import { NewProjectModal } from '@/components/dashboard/NewProjectModal';
import { ProjectSettingsModal } from '@/components/dashboard/ProjectSettingsModal';
import { InvoiceModal } from '@/components/dashboard/InvoiceModal';
import type { TwoFaState } from '@/components/dashboard/_ctx';

const TABS: { key: TabKey; icon: React.ElementType }[] = [
  { key: 'projects', icon: Server },
  { key: 'console', icon: Terminal },
  { key: 'troubleshoot', icon: Zap },
  { key: 'overview', icon: KeyRound },
  { key: 'telemetry', icon: Activity },
  { key: 'watchdog', icon: AlertTriangle },
  { key: 'logs', icon: ScrollText },
  { key: 'sdk', icon: Download },
  { key: 'builder', icon: Layers },
  { key: 'billing', icon: CreditCard },
  { key: 'affiliate', icon: Percent },
  { key: 'settings', icon: Settings },
];

/* =============================================================== */
export default function DashboardPage() {
  const router = useRouter();
  const { show, node } = useToast();
  const D = useT().dash;
  const BUILD_STAGES = D.buildStages;

  const [user, setUser] = useState<UserProfile | null>(null);
  const [licenses, setLicenses] = useState<License[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<TabKey>('projects');

  /* Projects & Servers state */
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProject, setSelectedProject] = useState<Project | null>(null);
  const [servers, setServers] = useState<ServerInstance[]>([]);
  const [loadingServers, setLoadingServers] = useState(false);
  const [dispatchingAction, setDispatchingAction] = useState<string | null>(null);

  /* txAdmin Console state */
  const [consoleInput, setConsoleInput] = useState('');
  const [consoleLogs, setConsoleLogs] = useState<Array<{ id: number; time: string; tag: string; text: string; tone: 'info' | 'warn' | 'error' | 'cmd' }>>([
    { id: 1, time: '19:42:01', tag: 'Core', text: 'FloV:MP Server Runtime v1.0.4 started (Build b3307 unhooked)', tone: 'info' },
    { id: 2, time: '19:42:02', tag: 'Network', text: 'UDP 7788 listening on 0.0.0.0:7788 (1500 max players)', tone: 'info' },
    { id: 3, time: '19:42:03', tag: 'AntiCheat', text: 'FloV:Shield security invariants active (Speed, Teleport, Weapon whitelists)', tone: 'info' },
    { id: 4, time: '19:42:04', tag: 'Database', text: 'MariaDB pool connected (derzhava_rp @ 188.127.229.224)', tone: 'info' },
    { id: 5, time: '19:42:05', tag: 'Agent', text: 'RemoteServerAgent connected to Cloud Control Plane (Token: agnt_live_prod_99f48a)', tone: 'info' },
    { id: 6, time: '19:42:15', tag: 'Telemetry', text: 'Heartbeat tick sent -> 60.0 Hz tickrate, 60.0 FPS, 384 MB CoreCLR', tone: 'info' },
  ]);

  /* AI Troubleshooter state */
  const [troubleshootText, setTroubleshootText] = useState('');
  const [diagnosing, setDiagnosing] = useState(false);
  const [diagnosticResult, setDiagnosticResult] = useState<any>(null);

  const [showKeyId, setShowKeyId] = useState<number | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  /* IP modal */
  const [ipLicense, setIpLicense] = useState<License | null>(null);
  const [ipValue, setIpValue] = useState('');
  const [ipName, setIpName] = useState('');
  const [ipErr, setIpErr] = useState('');
  const [savingIp, setSavingIp] = useState(false);

  /* project settings modal state */
  const [settingsModalOpen, setSettingsModalOpen] = useState(false);
  const [settingHwid, setSettingHwid] = useState<'strict' | 'lenient' | 'disabled'>('lenient');
  const [settingVpn, setSettingVpn] = useState(true);
  const [settingMaxAccs, setSettingMaxAccs] = useState(3);
  const [settingDiscord, setSettingDiscord] = useState('');
  const [settingTgToken, setSettingTgToken] = useState('');
  const [settingTgChat, setSettingTgChat] = useState('');
  const [settingAlertsEnabled, setSettingAlertsEnabled] = useState(true);
  const [savingSettings, setSavingSettings] = useState(false);
  const [testingWebhook, setTestingWebhook] = useState(false);

  /* resources state */
  const [resources, setResources] = useState<ResourceItem[]>([]);
  const [loadingResources, setLoadingResources] = useState(false);
  const [resourceActionLoading, setResourceActionLoading] = useState<string | null>(null);

  /* live SSE state */
  const [sseActive, setSseActive] = useState(false);
  const eventSourceRef = useRef<EventSource | null>(null);

  /* new project modal */
  const [newProjOpen, setNewProjOpen] = useState(false);
  const [newProjName, setNewProjName] = useState('');
  const [newProjSlug, setNewProjSlug] = useState('');
  const [newProjPlan, setNewProjPlan] = useState('enterprise');
  const [creatingProj, setCreatingProj] = useState(false);

  /* new license modal */
  const [newLicOpen, setNewLicOpen] = useState(false);
  const [newPlan, setNewPlan] = useState('business');
  const [newName, setNewName] = useState('');
  const [newIp, setNewIp] = useState('');
  const [creatingLic, setCreatingLic] = useState(false);

  /* billing */
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [payingId, setPayingId] = useState<number | null>(null);
  const [invoiceOpen, setInvoiceOpen] = useState(false);
  const [invPlan, setInvPlan] = useState('business');
  const [invPeriod, setInvPeriod] = useState<'monthly' | 'halfYear' | 'year'>('monthly');
  const [invMethod, setInvMethod] = useState<'card' | 'sbp' | 'crypto'>('card');
  const [creatingInvoice, setCreatingInvoice] = useState(false);

  /* telemetry */
  const [telemetry, setTelemetry] = useState<TelemetryPoint[]>([]);
  const [loadingTelemetry, setLoadingTelemetry] = useState(false);
  const [sendingHb, setSendingHb] = useState(false);

  /* builder */
  const [bProject, setBProject] = useState('Florida V');
  const [bColor, setBColor] = useState('#ff3d8a');
  const [bIp, setBIp] = useState('188.127.229.224');
  const [bPort, setBPort] = useState('7788');
  const [building, setBuilding] = useState(false);
  const [buildStage, setBuildStage] = useState(0);
  const [buildResult, setBuildResult] = useState<LauncherBuildResult | null>(null);
  const stageTimers = useRef<ReturnType<typeof setTimeout>[]>([]);

  /* 2FA (TOTP) — /api/v1/account/2fa */
  const [twoFa, setTwoFa] = useState<TwoFaState>({ enabled: false, loading: true, setup: null, code: '', busy: false, err: '' });

  const load2fa = async () => {
    try {
      const r = await fetch('/api/v1/account/2fa');
      const d = await r.json();
      setTwoFa((s) => ({ ...s, enabled: !!d.enabled, loading: false }));
    } catch {
      setTwoFa((s) => ({ ...s, loading: false }));
    }
  };

  const start2fa = async () => {
    setTwoFa((s) => ({ ...s, busy: true, err: '' }));
    try {
      const r = await fetch('/api/v1/account/2fa', { method: 'POST' });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || D.toast.err);
      setTwoFa((s) => ({ ...s, setup: { secret: d.secret, otpauthUri: d.otpauthUri }, code: '', busy: false }));
    } catch (e: any) {
      setTwoFa((s) => ({ ...s, busy: false, err: e.message }));
    }
  };

  const confirm2fa = async () => {
    setTwoFa((s) => ({ ...s, busy: true, err: '' }));
    try {
      const r = await fetch('/api/v1/account/2fa/enable', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: twoFa.code.trim() }),
      });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || D.toast.twoFaErr);
      setTwoFa({ enabled: true, loading: false, setup: null, code: '', busy: false, err: '' });
      show(D.toast.twoFaOn);
    } catch (e: any) {
      setTwoFa((s) => ({ ...s, busy: false, err: e.message }));
    }
  };

  const disable2fa = async () => {
    setTwoFa((s) => ({ ...s, busy: true, err: '' }));
    try {
      const r = await fetch('/api/v1/account/2fa/disable', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: twoFa.code.trim() }),
      });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || D.toast.twoFaErr);
      setTwoFa({ enabled: false, loading: false, setup: null, code: '', busy: false, err: '' });
      show(D.toast.twoFaOff);
    } catch (e: any) {
      setTwoFa((s) => ({ ...s, busy: false, err: e.message }));
    }
  };

  /* ------------------------------------------------------------- */
  useEffect(() => {
    void load2fa();
    void loadDashboard();
    return () => {
      stageTimers.current.forEach(clearTimeout);
      eventSourceRef.current?.close();
    };
  }, []);

  useEffect(() => {
    if (tab === 'billing') void loadInvoices();
    if (tab === 'telemetry') void loadTelemetry();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, licenses]);

  const loadDashboard = async () => {
    try {
      setLoading(true);
      const userRes = await fetch('/api/auth/me');
      if (!userRes.ok) {
        router.push('/auth/login');
        return;
      }
      const userData = await userRes.json();
      setUser(userData.user);

      const licRes = await fetch('/api/v1/license/list');
      if (licRes.ok) {
        const licData = await licRes.json();
        const lics: License[] = licData.licenses || [];
        setLicenses(lics);
        if (lics[0]?.bound_ip && lics[0].bound_ip !== '0.0.0.0') setBIp(lics[0].bound_ip);
      }

      const projRes = await fetch('/api/v1/projects');
      if (projRes.ok) {
        const projData = await projRes.json();
        const projs: Project[] = projData.projects || [];
        setProjects(projs);
        if (projs.length > 0) {
          setSelectedProject(projs[0]);
          void loadServers(projs[0].id);
        }
      }
    } catch {
      show(D.toast.errLoad, 'error');
    } finally {
      setLoading(false);
    }
  };

  const loadServers = async (projectId: number) => {
    setLoadingServers(true);
    try {
      const res = await fetch(`/api/v1/projects/${projectId}/servers`);
      if (res.ok) {
        const data = await res.json();
        const srvs = data.servers || [];
        setServers(srvs);
        if (srvs.length > 0) {
          void loadResources(srvs[0].id);
        }
      }
    } catch {
      show(D.toast.errLoadServers, 'error');
    } finally {
      setLoadingServers(false);
    }
  };

  const handleSelectProject = (proj: Project) => {
    setSelectedProject(proj);
    void loadServers(proj.id);
  };

  const openProjectSettings = (p: Project) => {
    setSettingHwid(p.hwid_policy || 'lenient');
    setSettingVpn(p.allow_vpn !== undefined ? p.allow_vpn : true);
    setSettingMaxAccs(p.max_accounts_per_hwid || 3);
    setSettingDiscord(p.discord_webhook_url || '');
    setSettingTgToken(p.telegram_webhook_token || '');
    setSettingTgChat(p.telegram_chat_id || '');
    setSettingAlertsEnabled(p.webhook_alerts_enabled !== undefined ? p.webhook_alerts_enabled : true);
    setSettingsModalOpen(true);
  };

  const saveProjectSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedProject) return;
    setSavingSettings(true);
    try {
      const res = await fetch(`/api/v1/projects/${selectedProject.id}/settings`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          hwidPolicy: settingHwid,
          allowVpn: settingVpn,
          maxAccountsPerHwid: settingMaxAccs,
          discordWebhookUrl: settingDiscord,
          telegramWebhookToken: settingTgToken,
          telegramChatId: settingTgChat,
          webhookAlertsEnabled: settingAlertsEnabled,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errSave);
      show(D.toast.settingsSaved);
      setSettingsModalOpen(false);
      loadDashboard();
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setSavingSettings(false);
    }
  };

  const testWebhooks = async (type: 'discord' | 'telegram' | 'all') => {
    if (!selectedProject) return;
    setTestingWebhook(true);
    try {
      const res = await fetch(`/api/v1/projects/${selectedProject.id}/webhooks/test`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ type }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errSend);
      show(data.message || D.toast.alertsSent);
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setTestingWebhook(false);
    }
  };

  const loadResources = async (serverId: number) => {
    if (!selectedProject) return;
    setLoadingResources(true);
    try {
      const res = await fetch(`/api/v1/projects/${selectedProject.id}/servers/${serverId}/resources`);
      if (res.ok) {
        const data = await res.json();
        setResources(data.resources || []);
      }
    } finally {
      setLoadingResources(false);
    }
  };

  const handleResourceControl = async (
    serverId: number,
    resourceName: string,
    action: 'start' | 'stop' | 'restart'
  ) => {
    if (!selectedProject) return;
    setResourceActionLoading(`${resourceName}_${action}`);
    try {
      const res = await fetch(`/api/v1/projects/${selectedProject.id}/servers/${serverId}/resources`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ action, resourceName }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errResource);
      show(`${action.toUpperCase()} · ${D.toast.cmdToAgent}`);
      loadResources(serverId);
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setResourceActionLoading(null);
    }
  };

  const toggleSseStream = () => {
    if (sseActive) {
      eventSourceRef.current?.close();
      eventSourceRef.current = null;
      setSseActive(false);
      show(D.toast.sseOff);
    } else {
      try {
        const es = new EventSource('/api/v1/agent/stream');
        es.onmessage = (event) => {
          try {
            const data = JSON.parse(event.data);
            if (data && data.text) {
              setConsoleLogs((prev) => [
                ...prev,
                {
                  id: Date.now() + Math.random(),
                  time: data.time || new Date().toLocaleTimeString('ru-RU'),
                  tag: data.tag || 'Agent',
                  text: data.text,
                  tone: data.tone || 'info',
                },
              ]);
            }
          } catch {}
        };
        es.onerror = () => {
          es.close();
          setSseActive(false);
        };
        eventSourceRef.current = es;
        setSseActive(true);
        show(D.toast.sseOn);
      } catch {
        show(D.toast.sseErr, 'error');
      }
    }
  };

  const createProjectHandler = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingProj(true);
    try {
      const res = await fetch('/api/v1/projects', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: newProjName.trim(),
          slug: newProjSlug.trim() || newProjName.trim().toLowerCase().replace(/\s+/g, '-'),
          plan: newProjPlan,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errCreateProj);
      show(D.toast.projCreated);
      setNewProjOpen(false);
      setNewProjName('');
      setNewProjSlug('');
      loadDashboard();
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setCreatingProj(false);
    }
  };

  const handleDispatchCommand = async (
    serverId: number,
    command: 'restart' | 'stop' | 'broadcast',
    payload?: any
  ) => {
    setDispatchingAction(`${serverId}_${command}`);
    try {
      const res = await fetch('/api/v1/agent/command', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          serverId,
          command,
          payload:
            payload ||
            (command === 'broadcast'
              ? { message: D.console.chipRebootCmd.replace(/^broadcast /, '') }
              : undefined),
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errCmd);
      show(`${command.toUpperCase()} · ${D.toast.cmdQueued} #${data.commandId}`);
      const timeStr = new Date().toLocaleTimeString('ru-RU');
      setConsoleLogs((prev) => [
        ...prev,
        {
          id: Date.now(),
          time: timeStr,
          tag: 'ControlPlane',
          text: `${command.toUpperCase()} · ${D.toast.cmdToAgent} #${serverId}`,
          tone: 'cmd',
        },
      ]);
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setDispatchingAction(null);
    }
  };

  const sendConsoleCommand = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!consoleInput.trim()) return;
    const input = consoleInput.trim();
    setConsoleInput('');

    const targetServerId = servers[0]?.id || 1;
    const timeStr = new Date().toLocaleTimeString('ru-RU');

    setConsoleLogs((prev) => [
      ...prev,
      { id: Date.now(), time: timeStr, tag: 'Operator', text: `> ${input}`, tone: 'cmd' },
    ]);

    if (input.startsWith('say ') || input.startsWith('broadcast ')) {
      const msg = input.replace(/^(say|broadcast)\s+/, '');
      await handleDispatchCommand(targetServerId, 'broadcast', { message: msg });
    } else if (input === 'restart') {
      await handleDispatchCommand(targetServerId, 'restart');
    } else if (input === 'stop') {
      await handleDispatchCommand(targetServerId, 'stop');
    } else {
      await handleDispatchCommand(targetServerId, 'broadcast', { message: input });
    }
  };

  const runTroubleshoot = async (sampleLogs?: string) => {
    const textToAnalyze = sampleLogs || troubleshootText;
    if (!textToAnalyze.trim()) {
      show(D.toast.pasteLogs, 'error');
      return;
    }
    setDiagnosing(true);
    setDiagnosticResult(null);
    try {
      const res = await fetch('/api/v1/ai/troubleshoot', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ logs: textToAnalyze }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errAnalyze);
      setDiagnosticResult(data.diagnosis);
      show(D.toast.aiDone);
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setDiagnosing(false);
    }
  };

  const loadInvoices = async () => {
    setLoadingInvoices(true);
    try {
      const res = await fetch('/api/v1/billing/invoices');
      if (res.ok) setInvoices((await res.json()).invoices || []);
    } finally {
      setLoadingInvoices(false);
    }
  };

  const loadTelemetry = async () => {
    if (licenses.length === 0) return;
    setLoadingTelemetry(true);
    try {
      const res = await fetch(
        `/api/v1/telemetry/stats?key=${encodeURIComponent(licenses[0].license_key)}`
      );
      if (res.ok) setTelemetry((await res.json()).stats || []);
    } finally {
      setLoadingTelemetry(false);
    }
  };

  const copy = (text: string) => {
    navigator.clipboard?.writeText(text).catch(() => {});
    setCopied(text);
    setTimeout(() => setCopied((c) => (c === text ? null : c)), 1800);
  };

  /* IP binding */
  const openIpModal = (lic: License) => {
    setIpLicense(lic);
    setIpValue(lic.bound_ip === '0.0.0.0' ? '' : lic.bound_ip);
    setIpName(lic.server_name);
    setIpErr('');
  };
  const saveIp = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!ipLicense) return;
    setIpErr('');
    setSavingIp(true);
    try {
      const res = await fetch('/api/v1/license/bind-ip', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: ipLicense.id,
          serverIp: ipValue.trim() || '0.0.0.0',
          serverName: ipName.trim() || ipLicense.server_name,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errBindIp);
      show(data.message || D.toast.ipSaved);
      setIpLicense(null);
      loadDashboard();
    } catch (err: any) {
      setIpErr(err.message);
    } finally {
      setSavingIp(false);
    }
  };

  /* create license */
  const createLicense = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingLic(true);
    try {
      const res = await fetch('/api/v1/license/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          plan: newPlan,
          serverName: newName.trim() || `${user?.username} RP Server`,
          boundIp: newIp.trim() || '0.0.0.0',
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errCreateLic);
      show(data.message || D.toast.licActivated);
      setNewLicOpen(false);
      setNewName('');
      setNewIp('');
      loadDashboard();
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setCreatingLic(false);
    }
  };

  /* billing */
  const payInvoice = async (invoiceId: number) => {
    setPayingId(invoiceId);
    try {
      const res = await fetch('/api/v1/billing/pay', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ invoiceId }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errPay);
      show(data.message || D.toast.payConfirmed);
      loadInvoices();
      loadDashboard();
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setPayingId(null);
    }
  };

  const createInvoice = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingInvoice(true);
    try {
      const res = await fetch('/api/v1/billing/create-invoice', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: licenses[0]?.id,
          plan: invPlan,
          period: invPeriod,
          paymentMethod: invMethod,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errInvoice);
      setInvoiceOpen(false);
      show(`${D.toast.invoiceIssued} #${data.invoiceId} · ${Number(data.amount).toLocaleString('ru-RU')} ₽`);
      loadInvoices();
    } catch (err: any) {
      show(err.message, 'error');
    } finally {
      setCreatingInvoice(false);
    }
  };

  /* telemetry heartbeat */
  const sendHeartbeat = async () => {
    if (licenses.length === 0) return;
    setSendingHb(true);
    try {
      const lic = licenses[0];
      const res = await fetch('/api/v1/telemetry/heartbeat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseKey: lic.license_key,
          players: Math.floor(Math.random() * 8) + 1,
          maxPlayers: lic.max_players,
          tickRate: 60,
          memoryMb: Math.floor(Math.random() * 20) + 240,
          fps: 60,
        }),
      });
      if (res.ok) {
        show(D.toast.hbRecorded);
        loadTelemetry();
      } else {
        show(D.toast.errTelemetry, 'error');
      }
    } finally {
      setSendingHb(false);
    }
  };

  /* launcher build */
  const buildLauncher = async (e: React.FormEvent) => {
    e.preventDefault();
    if (licenses.length === 0) {
      show(D.toast.needActiveLic, 'error');
      return;
    }
    setBuilding(true);
    setBuildResult(null);
    setBuildStage(0);
    stageTimers.current.forEach(clearTimeout);
    stageTimers.current = [1, 2].map((i) =>
      setTimeout(() => setBuildStage(i), i * 700)
    );

    try {
      const lic = licenses[0];
      const res = await fetch('/api/v1/launcher/build', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: lic.id,
          projectName: bProject,
          primaryColor: bColor,
          serverIp: bIp,
          serverPort: Number(bPort),
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || D.toast.errBuild);
      await new Promise((r) => setTimeout(r, 1500));
      setBuildStage(3);
      setBuildResult(data);
      show(D.toast.launcherBuilt);
    } catch (err: any) {
      show(err.message, 'error');
      setBuildStage(0);
    } finally {
      setBuilding(false);
    }
  };

  /* ------------------------------------------------------------- */
  const primaryLic = licenses[0];
  const isIpBound = primaryLic && primaryLic.bound_ip !== '0.0.0.0';
  const promoCode = `FLOV-${(user?.username || 'REF20').toUpperCase()}`;
  const latest = telemetry.length ? telemetry[telemetry.length - 1] : null;

  const onboarding = useMemo(
    () => [
      { done: true, title: D.overview.stepAccount, note: user?.email ?? '' },
      { done: !!primaryLic, title: D.overview.stepLicense, note: primaryLic ? `${D.overview.stepPlan}: ${primaryLic.plan}` : D.overview.stepLicenseNo },
      { done: !!isIpBound, title: D.overview.stepIp, note: isIpBound ? primaryLic!.bound_ip : D.overview.stepIpNo, warn: !isIpBound },
      { done: true, title: D.overview.stepNode, note: 'UDP 7788 · FastDL' },
    ],
    [user, primaryLic, isIpBound]
  );

  if (loading) {
    return (
      <div className="flex min-h-[70vh] flex-col items-center justify-center gap-3 text-brand">
        <Spinner className="h-8 w-8" />
        <p className="text-sm text-slate-400">{D.loading}</p>
      </div>
    );
  }

  const ctx = {
    D, BUILD_STAGES, primaryLic, isIpBound, promoCode, latest, onboarding,
    user, licenses, tab, setTab,
    projects, selectedProject, servers, loadingServers, dispatchingAction,
    consoleInput, setConsoleInput, consoleLogs, setConsoleLogs,
    troubleshootText, setTroubleshootText, diagnosing, diagnosticResult,
    showKeyId, setShowKeyId, copied,
    ipLicense, setIpLicense, ipValue, setIpValue, ipName, setIpName, ipErr, savingIp,
    settingsModalOpen, setSettingsModalOpen,
    settingHwid, setSettingHwid, settingVpn, setSettingVpn, settingMaxAccs, setSettingMaxAccs,
    settingDiscord, setSettingDiscord, settingTgToken, setSettingTgToken,
    settingTgChat, setSettingTgChat, settingAlertsEnabled, setSettingAlertsEnabled,
    savingSettings, testingWebhook,
    resources, loadingResources, resourceActionLoading, sseActive,
    newProjOpen, setNewProjOpen, newProjName, setNewProjName, newProjSlug, setNewProjSlug,
    newProjPlan, setNewProjPlan, creatingProj,
    newLicOpen, setNewLicOpen, newPlan, setNewPlan, newName, setNewName, newIp, setNewIp, creatingLic,
    invoices, loadingInvoices, payingId, invoiceOpen, setInvoiceOpen,
    invPlan, setInvPlan, invPeriod, setInvPeriod, invMethod, setInvMethod, creatingInvoice,
    telemetry, loadingTelemetry, sendingHb,
    bProject, setBProject, bColor, setBColor, bIp, setBIp, bPort, setBPort,
    building, buildStage, buildResult,
    twoFa, setTwoFa, start2fa, confirm2fa, disable2fa,
    loadServers, handleSelectProject, openProjectSettings, saveProjectSettings, testWebhooks,
    loadResources, handleResourceControl, toggleSseStream, createProjectHandler,
    handleDispatchCommand, sendConsoleCommand, runTroubleshoot, loadInvoices, loadTelemetry,
    copy, openIpModal, saveIp, createLicense, payInvoice, createInvoice, sendHeartbeat, buildLauncher,
  };

  return (
    <DashboardProvider value={ctx}>
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      {node}

      {/* Header */}
      <div className="card card-edge mb-6 flex flex-col gap-4 rounded-2xl p-6 sm:flex-row sm:items-center sm:justify-between sm:p-7">
        <div className="flex items-center gap-3.5">
          <span className="block h-11 w-11 overflow-hidden rounded-xl border border-white/10 bg-ink-800">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="" className="h-full w-full object-cover" />
          </span>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-semibold tracking-tight text-white sm:text-xl">{D.hello}, {user?.username}</h1>
              <Badge tone={user?.role === 'admin' ? 'red' : 'brand'}>
                {user?.role === 'admin' ? D.roleAdmin : D.roleClient}
              </Badge>
            </div>
            <p className="mt-0.5 text-[12px] text-white/45">
              {D.headerSub}
            </p>
          </div>
        </div>
        <button onClick={() => setNewLicOpen(true)} className="btn btn-primary h-10 px-4 text-xs">
          <Plus className="h-4 w-4" />
          {D.newLicense}
        </button>
      </div>

      {/* Tabs */}
      <div className="mb-6 flex gap-1 overflow-x-auto border-b border-white/[0.08] pb-2 no-scrollbar">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`flex shrink-0 items-center gap-2 rounded-lg px-3.5 py-2 font-mono text-[11px] font-semibold uppercase tracking-wider transition-colors ${
              tab === t.key
                ? 'bg-white/[0.06] text-white'
                : 'text-white/40 hover:bg-white/[0.04] hover:text-white/80'
            }`}
          >
            <t.icon className="h-4 w-4" style={{ color: tab === t.key ? 'var(--brand)' : undefined }} />
            {D.tabs[t.key]}
          </button>
        ))}
      </div>

      {/* ============ PROJECTS & SERVERS ============ */}
      {tab === 'projects' && <ProjectsTab />}

      {/* ============ txAdmin CONSOLE ============ */}
      {tab === 'console' && <ConsoleTab />}

      {/* ============ AI TROUBLESHOOTER ============ */}
      {tab === 'troubleshoot' && <TroubleshootTab />}

      {/* ============ OVERVIEW ============ */}
      {tab === 'overview' && <OverviewTab />}

      {/* ============ TELEMETRY ============ */}
      {tab === 'telemetry' && <TelemetryTab />}

      {/* ============ WATCHDOG & CRASHES ============ */}
      {tab === 'watchdog' && <WatchdogTab />}

      {/* ============ LOGS ============ */}
      {tab === 'logs' && <LogsTab />}

      {/* ============ DOWNLOADS & SDK ============ */}
      {tab === 'sdk' && <SdkTab />}

      {/* ============ BUILDER ============ */}
      {tab === 'builder' && <BuilderTab />}

      {/* ============ BILLING ============ */}
      {tab === 'billing' && <BillingTab />}

      {/* ============ AFFILIATE ============ */}
      {tab === 'affiliate' && <AffiliateTab />}

      {/* ============ SETTINGS ============ */}
      {tab === 'settings' && <SettingsTab />}

      {/* ============ MODALS ============ */}
      <IpBindModal />

      <NewLicenseModal />

      <NewProjectModal />

      {/* Project Settings & Webhook Modal */}
      <ProjectSettingsModal />

      <InvoiceModal />
    </div>
    </DashboardProvider>
  );
}
