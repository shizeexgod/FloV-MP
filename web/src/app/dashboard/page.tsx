'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import {
  Activity,
  AlertTriangle,
  BarChart3,
  CreditCard,
  Download,
  KeyRound,
  Layers,
  LogOut,
  Menu,
  Percent,
  Plug,
  RefreshCw,
  ScrollText,
  Server,
  Settings,
  Terminal,
  X,
} from 'lucide-react';
import { Select, Spinner, useToast } from '@/components/ui';
import { useT } from '@/lib/i18n';

import {
  DashboardProvider,
  type License, type Project, type ResourceItem, type ServerInstance, type UserProfile,
  type Invoice, type TelemetryPoint, type LauncherBuildResult, type TabKey,
} from '@/components/dashboard/_ctx';
import { ProjectsTab } from '@/components/dashboard/ProjectsTab';
import { ConsoleTab } from '@/components/dashboard/ConsoleTab';
import { OverviewTab } from '@/components/dashboard/OverviewTab';
import { TelemetryTab } from '@/components/dashboard/TelemetryTab';
import { SdkTab } from '@/components/dashboard/SdkTab';
import { BuilderTab } from '@/components/dashboard/BuilderTab';
import { AffiliateTab } from '@/components/dashboard/AffiliateTab';
import { AnalyticsTab } from '@/components/dashboard/AnalyticsTab';
import { WatchdogTab } from '@/components/dashboard/WatchdogTab';
import { LogsTab } from '@/components/dashboard/LogsTab';
import { ApiTab } from '@/components/dashboard/ApiTab';
import { SettingsTab } from '@/components/dashboard/SettingsTab';
import { BillingTab } from '@/components/dashboard/BillingTab';
import { NoProjectGate } from '@/components/dashboard/NoProjectGate';
import { IpBindModal } from '@/components/dashboard/IpBindModal';
import { NewProjectModal } from '@/components/dashboard/NewProjectModal';
import { ProjectSettingsModal } from '@/components/dashboard/ProjectSettingsModal';
import { InvoiceModal } from '@/components/dashboard/InvoiceModal';
import type { TwoFaState } from '@/components/dashboard/_ctx';

// Без проекта доступны только его создание, оплата и настройки аккаунта.
// Рабочие инструменты не рисуют пустые графики и выдуманные показатели.
const UNGATED_TABS = new Set<TabKey>(['projects', 'billing', 'settings']);

const TAB_ICONS: Partial<Record<TabKey, React.ElementType>> = {
  projects: Server,
  console: Terminal,
  overview: KeyRound,
  telemetry: Activity,
  analytics: BarChart3,
  watchdog: AlertTriangle,
  logs: ScrollText,
  api: Plug,
  sdk: Download,
  builder: Layers,
  billing: CreditCard,
  affiliate: Percent,
  settings: Settings,
};

type NavGroupId = 'manage' | 'monitor' | 'tools' | 'finance' | 'account';
const NAV_GROUPS: { id: NavGroupId; keys: TabKey[] }[] = [
  { id: 'manage', keys: ['projects', 'overview'] },
  { id: 'monitor', keys: ['telemetry', 'analytics', 'watchdog', 'logs'] },
  { id: 'tools', keys: ['console', 'api', 'sdk', 'builder'] },
  { id: 'finance', keys: ['billing', 'affiliate'] },
  { id: 'account', keys: ['settings'] },
];

const VALID_TABS = new Set<TabKey>(NAV_GROUPS.flatMap((group) => group.keys));

/* =============================================================== */
export default function DashboardPage() {
  const router = useRouter();
  const pathname = usePathname();
  const { show, node } = useToast();
  const t = useT();
  const D = t.dash;
  const BUILD_STAGES = D.buildStages;

  const [user, setUser] = useState<UserProfile | null>(null);
  const [licenses, setLicenses] = useState<License[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<TabKey>('projects');
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  /* Projects & Servers state */
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProject, setSelectedProject] = useState<Project | null>(null);
  const [servers, setServers] = useState<ServerInstance[]>([]);
  const [loadingServers, setLoadingServers] = useState(false);
  const [dispatchingAction, setDispatchingAction] = useState<string | null>(null);

  /* txAdmin Console state */
  const [consoleInput, setConsoleInput] = useState('');
  const [consoleLogs, setConsoleLogs] = useState<Array<{ id: number; time: string; tag: string; text: string; tone: 'info' | 'warn' | 'error' | 'cmd' }>>([]);

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
  const [newProjPlan, setNewProjPlan] = useState('lifetime');
  const [creatingProj, setCreatingProj] = useState(false);

  /* new license modal */
  const [newLicOpen, setNewLicOpen] = useState(false);
  const [newPlan, setNewPlan] = useState('lifetime');
  const [newName, setNewName] = useState('');
  const [newIp, setNewIp] = useState('');
  const [creatingLic, setCreatingLic] = useState(false);

  /* billing */
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [payingId, setPayingId] = useState<number | null>(null);
  const [invoiceOpen, setInvoiceOpen] = useState(false);
  const [invPlan, setInvPlan] = useState('lifetime');
  const [invPeriod, setInvPeriod] = useState<'monthly' | 'halfYear' | 'year'>('monthly');
  const [invMethod, setInvMethod] = useState<'card' | 'sbp' | 'crypto'>('card');
  const [creatingInvoice, setCreatingInvoice] = useState(false);

  /* telemetry */
  const [telemetry, setTelemetry] = useState<TelemetryPoint[]>([]);
  const [loadingTelemetry, setLoadingTelemetry] = useState(false);
  const [sendingHb, setSendingHb] = useState(false);

  /* builder */
  const [bProject, setBProject] = useState('');
  const [bColor, setBColor] = useState('#ff3d8a');
  const [bIp, setBIp] = useState('');
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

  /* Agent API key rotation */
  const [rotatingKey, setRotatingKey] = useState(false);
  const rotateApiKey = async () => {
    if (!selectedProject) return;
    setRotatingKey(true);
    try {
      const r = await fetch(`/api/v1/projects/${selectedProject.id}/api-key`, { method: 'POST' });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || D.toast.err);
      show(D.api.rotated);
      await loadDashboard();
    } catch (e: any) {
      show(e.message, 'error');
    } finally {
      setRotatingKey(false);
    }
  };

  const selectTab = (nextTab: TabKey) => {
    setTab(nextTab);
    setMobileNavOpen(false);
    const params = new URLSearchParams(window.location.search);
    params.set('section', nextTab);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  };

  const logout = async () => {
    await fetch('/api/auth/logout', { method: 'POST' }).catch(() => null);
    router.replace('/');
    router.refresh();
  };

  /* ------------------------------------------------------------- */
  useEffect(() => {
    const requestedTab = new URLSearchParams(window.location.search).get('section') as TabKey | null;
    if (requestedTab && VALID_TABS.has(requestedTab)) setTab(requestedTab);
    void load2fa();
    void loadDashboard();
    return () => {
      stageTimers.current.forEach(clearTimeout);
      eventSourceRef.current?.close();
    };
  }, []);

  useEffect(() => {
    document.body.style.overflow = mobileNavOpen ? 'hidden' : '';
    return () => {
      document.body.style.overflow = '';
    };
  }, [mobileNavOpen]);

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
          setBProject((current) => current || projs[0].name);
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
    if (licenses.length === 0) {
      // Сначала оформляется реальный счёт. Не создаём фиктивную Lifetime-лицензию
      // и не показываем проект как активный до подтверждённой оплаты.
      setNewProjOpen(false);
      setInvoiceOpen(true);
      return;
    }
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
          plan: 'lifetime',
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
  const hasWorkspaceAccess = licenses.some((license) => Number(license.is_active) === 1);
  const isIpBound = primaryLic && primaryLic.bound_ip !== '0.0.0.0';
  const promoCode = `FLOV-${(user?.username || 'REF20').toUpperCase()}`;
  const latest = telemetry.length ? telemetry[telemetry.length - 1] : null;

  const onboarding = useMemo(
    () => {
      const connectedServer = servers.find((server) => server.status === 'online');
      const knownServer = connectedServer ?? servers[0];
      return [
        { done: true, title: D.overview.stepAccount, note: user?.email ?? '' },
        { done: !!primaryLic, title: D.overview.stepLicense, note: primaryLic ? `${D.overview.stepPlan}: ${primaryLic.plan}` : D.overview.stepLicenseNo },
        { done: !!isIpBound, title: D.overview.stepIp, note: isIpBound ? primaryLic!.bound_ip : D.overview.stepIpNo, warn: !isIpBound },
        {
          done: !!connectedServer,
          title: D.overview.stepNode,
          note: connectedServer
            ? `${connectedServer.ip}:${connectedServer.port}`
            : knownServer
              ? D.overview.stepNodeOffline
              : D.overview.stepNodeNo,
          warn: !connectedServer,
        },
      ];
    },
    [D, user, primaryLic, isIpBound, servers]
  );

  useEffect(() => {
    if (loading || hasWorkspaceAccess || UNGATED_TABS.has(tab)) return;
    setTab('projects');
    const params = new URLSearchParams(window.location.search);
    params.set('section', 'projects');
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }, [hasWorkspaceAccess, loading, pathname, router, tab]);

  if (loading) {
    return (
      <div className="precision-dashboard grid min-h-dvh place-items-center bg-[#09090b] text-brand">
        <div className="flex flex-col items-center gap-3" role="status" aria-live="polite">
          <span className="grid h-12 w-12 place-items-center rounded-2xl border border-brand/20 bg-brand/[0.06]">
            <Spinner className="h-5 w-5" />
          </span>
          <p className="text-xs font-semibold text-white/40">{D.loading}</p>
        </div>
      </div>
    );
  }

  const ctx = {
    D, BUILD_STAGES, primaryLic, isIpBound, promoCode, latest, onboarding,
    user, licenses, tab, setTab: selectTab,
    projects, selectedProject, servers, loadingServers, dispatchingAction,
    consoleInput, setConsoleInput, consoleLogs, setConsoleLogs,
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
    rotatingKey, rotateApiKey,
    loadServers, handleSelectProject, openProjectSettings, saveProjectSettings, testWebhooks,
    loadResources, handleResourceControl, toggleSseStream, createProjectHandler,
    handleDispatchCommand, sendConsoleCommand, loadInvoices, loadTelemetry,
    copy, openIpModal, saveIp, createLicense, payInvoice, createInvoice, sendHeartbeat, buildLauncher,
  };

  const ActiveIcon = TAB_ICONS[tab] ?? Layers;
  const currentProjectName = selectedProject?.name || D.workspace.noProject;

  const renderNavigation = () => (
    <>
      <div className="px-3 pb-3 pt-3">
        <div className="mb-1.5 px-1 text-[9px] font-bold uppercase tracking-[0.18em] text-white/25">
          {D.workspace.currentProject}
        </div>
        <Select
          value={selectedProject ? String(selectedProject.id) : ''}
          onChange={(v) => {
            const project = projects.find((item) => item.id === Number(v));
            if (project) handleSelectProject(project);
          }}
          options={projects.map((p) => ({ value: String(p.id), label: p.name, meta: `/${p.slug}` }))}
          placeholder={D.workspace.noProject}
          ariaLabel={D.workspace.currentProject}
          disabled={projects.length === 0}
          triggerClassName="h-10 text-[12.5px]"
        />
      </div>
      <hr className="rule-soft mx-3" />

      <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label={D.workspace.sections}>
        {NAV_GROUPS.map((group) => {
          const visibleKeys = hasWorkspaceAccess
            ? group.keys
            : group.keys.filter((key) => UNGATED_TABS.has(key));
          if (visibleKeys.length === 0) return null;
          return (
          <div key={group.id} className="mt-5 first:mt-0">
            <div className="mb-1.5 px-2 text-[9px] font-bold uppercase tracking-[0.18em] text-white/25">
              {D.navGroups[group.id]}
            </div>
            <div className="space-y-0.5">
              {visibleKeys.map((key) => {
                const Icon = TAB_ICONS[key] ?? Layers;
                const selected = tab === key;
                return (
                  <button
                    key={key}
                    type="button"
                    data-section={key}
                    onClick={() => selectTab(key)}
                    aria-current={selected ? 'page' : undefined}
                    className={`group flex w-full items-center gap-3 rounded-xl px-2.5 py-2.5 text-left text-[11.5px] font-bold transition-colors duration-150 active:scale-[0.99] ${
                      selected
                        ? 'bg-brand/[0.12] text-white'
                        : 'text-white/45 hover:bg-white/[0.04] hover:text-white/80'
                    }`}
                  >
                    <Icon aria-hidden="true" className={`h-4 w-4 shrink-0 transition-colors ${selected ? 'text-brand' : 'text-white/25 group-hover:text-white/45'}`} />
                    <span className="min-w-0 flex-1 truncate">{D.tabs[key]}</span>
                  </button>
                );
              })}
            </div>
          </div>
          );
        })}
      </nav>
    </>
  );

  return (
    <DashboardProvider value={ctx}>
      <div className="precision-dashboard h-dvh overflow-hidden bg-[#09090b] text-white">
        {node}
        <div className="flex h-full min-w-0 gap-3 p-3">
          <aside className="hidden w-[240px] shrink-0 flex-col rounded-2xl border border-white/[0.07] bg-[#0d0d10] lg:flex">
            <div className="flex h-14 shrink-0 items-center px-4">
              <Link href="/" className="group flex items-center gap-2.5" aria-label="FloV:MP">
                <Image src="/branding/logo.png" alt="" width={28} height={28} className="h-7 w-7 object-contain transition-transform duration-200 group-hover:scale-105" />
                <span translate="no" className="text-sm font-extrabold text-white">FloV<span className="text-brand">:MP</span></span>
              </Link>
            </div>
            <hr className="rule-soft mx-3" />

            {renderNavigation()}

            <hr className="rule-soft mx-3" />
            <div className="shrink-0 p-3">
              <div className="flex items-center gap-3 rounded-xl p-2">
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-brand/10 text-[10px] font-extrabold uppercase text-brand">
                  {(user?.username || 'F').slice(0, 2)}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[11px] font-bold text-white/65">{user?.username}</span>
                  <span className="block truncate text-[9px] text-white/25">{user?.email}</span>
                </span>
                <button type="button" onClick={logout} aria-label={t.common.logout} title={t.common.logout} className="grid h-8 w-8 place-items-center rounded-lg text-white/25 transition-colors hover:bg-white/[0.05] hover:text-white/70">
                  <LogOut aria-hidden="true" className="h-4 w-4" />
                </button>
              </div>
            </div>
          </aside>

          <div className="flex min-w-0 flex-1 flex-col overflow-hidden rounded-2xl border border-white/[0.07] bg-[#0b0b0d] lg:border-0 lg:bg-transparent">
            <header className="flex h-14 shrink-0 items-center gap-3 rounded-t-2xl bg-[#0c0c0f] px-4 lg:hidden">
              <button type="button" onClick={() => setMobileNavOpen(true)} aria-label={D.workspace.openMenu} aria-expanded={mobileNavOpen} className="grid h-9 w-9 place-items-center rounded-lg border border-white/[0.08] text-white/55 transition-colors hover:border-white/[0.15] hover:text-white">
                <Menu aria-hidden="true" className="h-4 w-4" />
              </button>
              <Link href="/" className="flex items-center gap-2" aria-label="FloV:MP">
                <Image src="/branding/logo.png" alt="" width={28} height={28} className="h-7 w-7 object-contain" />
                <span translate="no" className="text-xs font-extrabold text-white">FloV<span className="text-brand">:MP</span></span>
              </Link>
            </header>

            <main className="min-w-0 flex-1 overflow-y-auto overscroll-contain">
              <div className="mx-auto w-full max-w-[1240px] p-4 sm:p-6 xl:p-8">
                <header className="mb-6 flex flex-col gap-4 pb-6 sm:flex-row sm:items-end sm:justify-between">
                  <div className="min-w-0">
                    <div className="mb-2 flex min-w-0 items-center gap-2 text-[10px] font-bold uppercase tracking-[0.16em] text-brand">
                      <span className="truncate">{currentProjectName}</span>
                      <span aria-hidden="true" className="text-white/15">/</span>
                      <span className="truncate text-white/35">{D.tabs[tab]}</span>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-brand/20 bg-brand/[0.07] text-brand">
                        <ActiveIcon aria-hidden="true" className="h-4 w-4" />
                      </span>
                      <div className="min-w-0">
                        <h1 className="truncate text-xl font-bold tracking-tight text-white sm:text-2xl">{D.tabs[tab]}</h1>
                        <p className="mt-1 text-xs leading-relaxed text-white/40">{D.tabDescriptions[tab]}</p>
                      </div>
                    </div>
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <button type="button" onClick={() => void loadDashboard()} className="btn btn-ghost h-9 flex-1 px-3.5 text-xs sm:flex-none">
                      <RefreshCw aria-hidden="true" className="h-3.5 w-3.5" />
                      {D.refresh}
                    </button>
                  </div>
                </header>
                <hr className="rule mb-6" />

                <section key={tab} data-dashboard-tab={tab} className="dashboard-section min-w-0 animate-view-in">
                  {!UNGATED_TABS.has(tab) && projects.length === 0 ? (
                    <NoProjectGate />
                  ) : (
                    <>
                      {tab === 'projects' && <ProjectsTab />}
                      {tab === 'overview' && <OverviewTab />}
                      {tab === 'console' && <ConsoleTab />}
                      {tab === 'telemetry' && <TelemetryTab />}
                      {tab === 'analytics' && <AnalyticsTab />}
                      {tab === 'watchdog' && <WatchdogTab />}
                      {tab === 'logs' && <LogsTab />}
                      {tab === 'api' && <ApiTab />}
                      {tab === 'sdk' && <SdkTab />}
                      {tab === 'builder' && <BuilderTab />}
                      {tab === 'billing' && <BillingTab />}
                      {tab === 'affiliate' && <AffiliateTab />}
                    </>
                  )}
                  {tab === 'settings' && <SettingsTab />}
                </section>
              </div>
            </main>
          </div>
        </div>

        {mobileNavOpen ? (
          <div className="fixed inset-0 z-[90] lg:hidden">
            <button type="button" onClick={() => setMobileNavOpen(false)} aria-label={D.workspace.closeMenu} className="absolute inset-0 bg-black/70" />
            <aside className="relative z-10 m-3 flex h-[calc(100%-1.5rem)] w-[min(88vw,320px)] flex-col rounded-2xl border border-white/[0.08] bg-[#0d0d10] shadow-2xl" role="dialog" aria-modal="true" aria-label={D.workspace.sections}>
              <div className="flex h-14 shrink-0 items-center justify-between px-4">
                <span className="flex items-center gap-2.5">
                  <Image src="/branding/logo.png" alt="" width={28} height={28} className="h-7 w-7 object-contain" />
                  <span translate="no" className="text-sm font-extrabold text-white">FloV<span className="text-brand">:MP</span></span>
                </span>
                <button type="button" onClick={() => setMobileNavOpen(false)} aria-label={D.workspace.closeMenu} className="grid h-9 w-9 place-items-center rounded-lg text-white/45 transition-colors hover:bg-white/[0.05] hover:text-white">
                  <X aria-hidden="true" className="h-4 w-4" />
                </button>
              </div>
              <hr className="rule-soft mx-3" />
              {renderNavigation()}
              <hr className="rule-soft mx-3" />
              <div className="shrink-0 p-3">
                <button type="button" onClick={logout} className="flex h-10 w-full items-center justify-center gap-2 rounded-xl border border-white/[0.08] text-xs font-bold text-white/55 transition-colors hover:border-white/[0.15] hover:text-white">
                  <LogOut aria-hidden="true" className="h-4 w-4" /> {t.common.logout}
                </button>
              </div>
            </aside>
          </div>
        ) : null}

        <IpBindModal />
        <NewProjectModal />
        <ProjectSettingsModal />
        <InvoiceModal />
      </div>
    </DashboardProvider>
  );
}
