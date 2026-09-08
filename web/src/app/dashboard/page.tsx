'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bell,
  Box,
  Check,
  CheckCircle2,
  ChevronDown,
  CircleDot,
  Copy,
  Cpu,
  CreditCard,
  Download,
  Eye,
  EyeOff,
  Gauge,
  Globe,
  HardDrive,
  HelpCircle,
  KeyRound,
  Layers,
  Percent,
  Play,
  PlayCircle,
  Plus,
  Radio,
  RefreshCw,
  Rocket,
  Send,
  Server,
  Settings2,
  Shield,
  ShieldCheck,
  Sliders,
  Square,
  StopCircle,
  Terminal,
  Users,
  Zap,
} from 'lucide-react';
import { Badge, FieldLabel, Modal, Spinner, useToast } from '@/components/ui';

/* ----------------------------- types ----------------------------- */
interface License {
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

interface UserProfile {
  id: number;
  username: string;
  email: string;
  role: string;
  telegram?: string;
  created_at?: string;
}
interface Invoice {
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
interface TelemetryPoint {
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
interface LauncherBuildResult {
  buildId: number;
  projectName: string;
  primaryColor: string;
  downloadUrl: string;
  config: { serverIp: string; serverPort: number; licenseKey: string };
  message: string;
}

type TabKey = 'projects' | 'console' | 'troubleshoot' | 'overview' | 'telemetry' | 'sdk' | 'builder' | 'billing' | 'affiliate';

const TABS: { key: TabKey; label: string; icon: React.ElementType }[] = [
  { key: 'projects', label: 'Проекты и Серверы', icon: Server },
  { key: 'console', label: 'txAdmin Консоль', icon: Terminal },
  { key: 'troubleshoot', label: 'AI Диагностика', icon: Zap },
  { key: 'overview', label: 'Ключи и статус', icon: KeyRound },
  { key: 'telemetry', label: 'Телеметрия VDS', icon: Activity },
  { key: 'sdk', label: 'Загрузки и SDK', icon: Download },
  { key: 'builder', label: 'Сборщик лаунчера', icon: Layers },
  { key: 'billing', label: 'Счета и биллинг', icon: CreditCard },
  { key: 'affiliate', label: 'Партнёрка (20%)', icon: Percent },
];

const COLOR_PRESETS = [
  { name: 'FloV Pink', hex: '#ff3d8a' },
  { name: 'Majestic Cyan', hex: '#00f0ff' },
  { name: 'Amber Gold', hex: '#f59e0b' },
  { name: 'Emerald', hex: '#10b981' },
  { name: 'Purple', hex: '#8b5cf6' },
];

const BUILD_STAGES = ['Генерация конфигурации…', 'Упаковка Electron + Native Bridge…', 'Подписание EXE…', 'Готово'];

const planTone = (plan: string) =>
  plan === 'enterprise' ? 'cyber' : plan === 'business' ? 'brand' : 'slate';

const dateShort = (s: string) => new Date(s).toLocaleDateString('ru-RU');
const timeShort = (s: string) => new Date(s).toLocaleTimeString('ru-RU');

/* =============================================================== */
export default function DashboardPage() {
  const router = useRouter();
  const { show, node } = useToast();

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
  const [bProject, setBProject] = useState('Держава Онлайн');
  const [bColor, setBColor] = useState('#ff3d8a');
  const [bIp, setBIp] = useState('188.127.229.224');
  const [bPort, setBPort] = useState('7788');
  const [building, setBuilding] = useState(false);
  const [buildStage, setBuildStage] = useState(0);
  const [buildResult, setBuildResult] = useState<LauncherBuildResult | null>(null);
  const stageTimers = useRef<ReturnType<typeof setTimeout>[]>([]);

  /* ------------------------------------------------------------- */
  useEffect(() => {
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
      show('Не удалось загрузить данные кабинета', 'error');
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
      show('Не удалось загрузить серверы проекта', 'error');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка сохранения');
      show('Настройки проекта и Webhook сохранены');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка отправки');
      show(data.message || 'Тестовые алерты отправлены');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка управления ресурсом');
      show(`Команда ${action.toUpperCase()} отправлена агенту`);
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
      show('Живой SSE стрим консоли отключен');
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
        show('Живой SSE стрим консоли активирован');
      } catch {
        show('Не удалось подключиться к SSE потоку', 'error');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка создания проекта');
      show('Новый проект успешно создан');
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
              ? { message: 'Внимание: техническая перезагрузка сервера через 60 секунд!' }
              : undefined),
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Ошибка отправки команды');
      show(`Команда ${command.toUpperCase()} поставлена в очередь (ID: ${data.commandId})`);
      const timeStr = new Date().toLocaleTimeString('ru-RU');
      setConsoleLogs((prev) => [
        ...prev,
        {
          id: Date.now(),
          time: timeStr,
          tag: 'ControlPlane',
          text: `Команда ${command.toUpperCase()} отправлена агенту сервера #${serverId}`,
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
      show('Вставьте фрагмент логов сервера для анализа', 'error');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка анализа логов');
      setDiagnosticResult(data.diagnosis);
      show('AI Диагностика завершена');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка привязки IP');
      show(data.message || 'IP-адрес сохранён');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка создания лицензии');
      show(data.message || 'Лицензия активирована');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка оплаты');
      show(data.message || 'Оплата подтверждена');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка выставления счёта');
      setInvoiceOpen(false);
      show(`Счёт №${data.invoiceId} на ${Number(data.amount).toLocaleString('ru-RU')} ₽ выставлен`);
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
        show('Heartbeat-пакет записан');
        loadTelemetry();
      } else {
        show('Ошибка записи телеметрии', 'error');
      }
    } finally {
      setSendingHb(false);
    }
  };

  /* launcher build */
  const buildLauncher = async (e: React.FormEvent) => {
    e.preventDefault();
    if (licenses.length === 0) {
      show('Для сборки нужна активная лицензия', 'error');
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
      if (!res.ok) throw new Error(data.error || 'Ошибка сборки лаунчера');
      await new Promise((r) => setTimeout(r, 1500));
      setBuildStage(3);
      setBuildResult(data);
      show('Кастомный лаунчер собран');
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
      { done: true, title: 'Аккаунт создан', note: user?.email ?? '' },
      { done: !!primaryLic, title: 'Лицензия активна', note: primaryLic ? `Тариф: ${primaryLic.plan}` : 'Не создана' },
      { done: !!isIpBound, title: 'IP сервера привязан', note: isIpBound ? primaryLic!.bound_ip : 'Требуется привязать', warn: !isIpBound },
      { done: true, title: 'Сетевой узел онлайн', note: 'UDP 7788 · FastDL' },
    ],
    [user, primaryLic, isIpBound]
  );

  if (loading) {
    return (
      <div className="flex min-h-[70vh] flex-col items-center justify-center gap-3 text-brand">
        <Spinner className="h-8 w-8" />
        <p className="text-sm text-slate-400">Загрузка панели управления FloV:MP…</p>
      </div>
    );
  }

  return (
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
              <h1 className="text-lg font-semibold tracking-tight text-white sm:text-xl">Здравствуйте, {user?.username}</h1>
              <Badge tone={user?.role === 'admin' ? 'red' : 'brand'}>
                {user?.role === 'admin' ? 'Администратор' : 'Клиент SaaS'}
              </Badge>
            </div>
            <p className="mt-0.5 text-[12px] text-white/45">
              Лицензии, телеметрия VDS, биллинг и брендированный лаунчер FloV:MP
            </p>
          </div>
        </div>
        <button onClick={() => setNewLicOpen(true)} className="btn btn-primary h-10 px-4 text-xs">
          <Plus className="h-4 w-4" />
          Новая лицензия
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
            {t.label}
          </button>
        ))}
      </div>

      {/* ============ PROJECTS & SERVERS ============ */}
      {tab === 'projects' && (
        <div className="relative space-y-8 animate-fade-in">
          {/* Project Switcher Bar */}
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Server className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">Проекты экосистемы FloV:MP</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                Иерархия: Аккаунт → Проект (Лицензия) → Игровые серверы (Prod / Dev / Test)
              </p>
            </div>
            <button
              onClick={() => setNewProjOpen(true)}
              className="btn btn-primary h-10 px-4 text-xs"
            >
              <Plus className="h-4 w-4" />
              Создать проект
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
                      /{p.slug} · {p.max_players} слотов
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
                  <span className="eyebrow text-brand">Активный проект</span>
                  <h3 className="mt-1 text-2xl font-black text-white">{selectedProject.name}</h3>
                  <p className="mt-1 font-mono text-xs text-slate-400">
                    ID: #{selectedProject.id} · Тариф: <span className="text-white uppercase">{selectedProject.plan}</span> · Лимит: <span className="text-brand">{selectedProject.max_players} слотов</span>
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
                    title="Скопировать License Key"
                  >
                    {copied === selectedProject.license_key ? (
                      <Check className="h-4 w-4 text-emeraldx" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </button>

                  <div className="rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2 font-mono text-xs text-slate-300">
                    <span className="text-slate-500 mr-2">Agent API Key:</span>
                    <strong className="text-brand">{selectedProject.api_key.slice(0, 16)}…</strong>
                  </div>
                  <button
                    onClick={() => copy(selectedProject.api_key)}
                    className="btn btn-ghost h-9 w-9 p-0"
                    title="Скопировать Agent API Key"
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
                    title="Настройки безопасности и Webhook"
                  >
                    <Sliders className="h-4 w-4" />
                    Настройки & Webhook
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Environments & Servers Section */}
          <div>
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h3 className="flex items-center gap-2 text-base font-bold text-white">
                  <Cpu className="h-5 w-5 text-cyber" />
                  Серверные среды (Environments)
                </h3>
                <p className="mt-0.5 text-xs text-slate-400">
                  txAdmin Cloud агент на каждом узле слушает команды и отсылает телеметрию
                </p>
              </div>
              <span className="font-mono text-xs text-slate-500">Серверов: {servers.length}</span>
            </div>

            {loadingServers ? (
              <div className="flex justify-center p-12 text-brand">
                <Spinner className="h-6 w-6" />
              </div>
            ) : servers.length === 0 ? (
              <div className="glass card-edge rounded-3xl p-10 text-center text-xs text-slate-400">
                <Server className="mx-auto h-8 w-8 text-slate-600 mb-2" />
                В этом проекте ещё нет запущенных серверов.
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
                          <span
                            className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase ${
                              isProd
                                ? 'border-emeraldx/30 bg-emeraldx/15 text-emeraldx'
                                : isDev
                                ? 'border-cyber/30 bg-cyber/15 text-cyber'
                                : 'border-violetx/30 bg-violetx/15 text-violetx'
                            }`}
                          >
                            <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                            {srv.environment}
                          </span>
                          <span className="flex items-center gap-1 font-mono text-[10px] text-slate-400">
                            <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
                            Agent Active
                          </span>
                        </div>

                        <h4 className="mt-3 text-base font-bold text-white">{srv.name}</h4>
                        <div className="mt-1 font-mono text-xs text-brand">
                          {srv.ip}:{srv.port}
                        </div>

                        <div className="mt-4 grid grid-cols-2 gap-2 rounded-xl border border-white/5 bg-ink-950/50 p-3 font-mono text-[11px]">
                          <div>
                            <span className="text-slate-500">Слоты:</span>{' '}
                            <strong className="text-white">{srv.max_players}</strong>
                          </div>
                          <div>
                            <span className="text-slate-500">Протокол:</span>{' '}
                            <strong className="text-cyber">UDP 7788</strong>
                          </div>
                          <div>
                            <span className="text-slate-500">Агент:</span>{' '}
                            <strong className="text-emeraldx">v1.0.4</strong>
                          </div>
                          <div>
                            <span className="text-slate-500">FastDL:</span>{' '}
                            <strong className="text-white">CDN Active</strong>
                          </div>
                        </div>
                      </div>

                      {/* Server Controls */}
                      <div className="mt-5 space-y-2 border-t border-white/[0.08] pt-4">
                        <div className="grid grid-cols-2 gap-2">
                          <button
                            onClick={() => handleDispatchCommand(srv.id, 'restart')}
                            disabled={isRestarting}
                            className="btn h-9 border border-amber-500/30 bg-amber-500/10 text-xs font-semibold text-amber-300 transition hover:bg-amber-500/20 disabled:opacity-50"
                          >
                            {isRestarting ? <Spinner className="h-3.5 w-3.5" /> : <RefreshCw className="h-3.5 w-3.5" />}
                            Перезапуск
                          </button>
                          <button
                            onClick={() => handleDispatchCommand(srv.id, 'stop')}
                            disabled={isStopping}
                            className="btn h-9 border border-red-500/30 bg-red-500/10 text-xs font-semibold text-red-400 transition hover:bg-red-500/20 disabled:opacity-50"
                          >
                            {isStopping ? <Spinner className="h-3.5 w-3.5" /> : <Square className="h-3.5 w-3.5" />}
                            Остановить
                          </button>
                        </div>
                        <button
                          onClick={() => handleDispatchCommand(srv.id, 'broadcast')}
                          disabled={isBroadcasting}
                          className="btn btn-ghost h-9 w-full text-xs font-semibold text-slate-300"
                        >
                          {isBroadcasting ? <Spinner className="h-3.5 w-3.5" /> : <Radio className="h-3.5 w-3.5 text-brand" />}
                          Анонс игрокам
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
                      Управление ресурсами сервера (Resource Manager)
                    </h4>
                    <p className="text-xs text-slate-400">
                      Запуск, остановка и перезапуск скриптов, карт и транспорта на лету без перезагрузки узла
                    </p>
                  </div>
                  <button
                    onClick={() => loadResources(servers[0].id)}
                    disabled={loadingResources}
                    className="btn btn-ghost h-9 px-3 text-xs"
                  >
                    <RefreshCw className={`h-3.5 w-3.5 ${loadingResources ? 'animate-spin text-brand' : ''}`} />
                    Обновить ресурсы
                  </button>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full min-w-[600px] text-left text-xs">
                    <thead>
                      <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                        <th className="pb-3 pr-3 font-semibold">Ресурс</th>
                        <th className="pb-3 pr-3 font-semibold">Тип</th>
                        <th className="pb-3 pr-3 font-semibold">Версия</th>
                        <th className="pb-3 pr-3 font-semibold">Статус</th>
                        <th className="pb-3 pr-3 text-right font-semibold">Управление</th>
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
                                {isRunning ? 'Работает' : 'Остановлен'}
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
                                      title="Перезапустить ресурс"
                                    >
                                      {isRestarting ? <Spinner className="h-3 w-3" /> : <RefreshCw className="h-3 w-3" />}
                                    </button>
                                    <button
                                      onClick={() => handleResourceControl(servers[0].id, res.name, 'stop')}
                                      disabled={isStopping}
                                      className="btn h-8 border border-red-500/30 bg-red-500/10 px-2.5 text-[11px] text-red-400 transition hover:bg-red-500/20"
                                      title="Остановить ресурс"
                                    >
                                      {isStopping ? <Spinner className="h-3 w-3" /> : <Square className="h-3 w-3" />}
                                    </button>
                                  </>
                                ) : (
                                  <button
                                    onClick={() => handleResourceControl(servers[0].id, res.name, 'start')}
                                    disabled={isStarting}
                                    className="btn h-8 border border-emeraldx/30 bg-emeraldx/10 px-2.5 text-[11px] text-emeraldx transition hover:bg-emeraldx/20"
                                    title="Запустить ресурс"
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
        </div>
      )}

      {/* ============ txAdmin CONSOLE ============ */}
      {tab === 'console' && (
        <div className="relative space-y-6 animate-fade-in">
          {/* Header */}
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Terminal className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">txAdmin Cloud Web Console</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                Прямой двухсторонний канал управления сервером FloV:MP через RemoteServerAgent
              </p>
            </div>
            <div className="flex items-center gap-3">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emeraldx/30 bg-emeraldx/15 px-3 py-1 font-mono text-[11px] font-bold text-emeraldx">
                <span className="h-2 w-2 rounded-full bg-emeraldx animate-pulse" />
                Agent Connected (UDP 7788)
              </span>
              <button
                onClick={toggleSseStream}
                className={`btn h-9 px-3 text-xs font-semibold transition ${
                  sseActive
                    ? 'bg-emeraldx text-ink-950 font-bold'
                    : 'btn-ghost text-slate-300'
                }`}
                title="Реальное время без задержек по Server-Sent Events"
              >
                <Radio className={`h-3.5 w-3.5 ${sseActive ? 'animate-pulse' : ''}`} />
                {sseActive ? 'SSE Активен' : 'Включить SSE'}
              </button>
              <button
                onClick={() =>
                  setConsoleLogs([
                    {
                      id: Date.now(),
                      time: new Date().toLocaleTimeString('ru-RU'),
                      tag: 'System',
                      text: 'Консоль очищена',
                      tone: 'info',
                    },
                  ])
                }
                className="btn btn-ghost h-9 px-3 text-xs"
              >
                Очистить
              </button>
            </div>
          </div>

          {/* Quick Command Chips */}
          <div className="flex flex-wrap gap-2">
            {[
              { label: 'Перезагрузка с анонсом', cmd: 'broadcast Внимание: перезагрузка через 5 минут!' },
              { label: 'Запросить статус игроков', cmd: 'status' },
              { label: 'Принудительный GC', cmd: 'coreclr gc collect' },
              { label: 'Проверить BattlEye', cmd: 'battleye status' },
            ].map((qc) => (
              <button
                key={qc.label}
                onClick={() => setConsoleInput(qc.cmd)}
                className="rounded-xl border border-white/10 bg-white/[0.03] px-3 py-1.5 font-mono text-[11px] text-slate-300 transition hover:border-brand/40 hover:bg-brand/10 hover:text-brand"
              >
                {qc.label}
              </button>
            ))}
          </div>

          {/* Terminal Box */}
          <div className="rounded-3xl border border-white/15 bg-ink-950/90 p-5 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between border-b border-white/10 pb-3 font-mono text-xs text-slate-500">
              <div className="flex items-center gap-2">
                <span className="h-3 w-3 rounded-full bg-red-500/80" />
                <span className="h-3 w-3 rounded-full bg-amber-500/80" />
                <span className="h-3 w-3 rounded-full bg-emeraldx/80" />
                <span className="ml-2 text-slate-400">flovmp-control-plane://server-agent.flovmp.net</span>
              </div>
              <span className="text-[11px]">TTY-1 (C# CoreCLR)</span>
            </div>

            <div className="mt-4 max-h-[480px] min-h-[340px] space-y-2 overflow-y-auto font-mono text-xs leading-relaxed no-scrollbar">
              {consoleLogs.map((log) => (
                <div key={log.id} className="flex items-start gap-3">
                  <span className="text-slate-600">{log.time}</span>
                  <span
                    className={`rounded px-1.5 py-0.5 text-[10px] uppercase font-bold ${
                      log.tone === 'cmd'
                        ? 'bg-brand/20 text-brand'
                        : log.tone === 'warn'
                        ? 'bg-amber-500/20 text-amber-300'
                        : log.tone === 'error'
                        ? 'bg-red-500/20 text-red-400'
                        : 'bg-white/10 text-cyber'
                    }`}
                  >
                    {log.tag}
                  </span>
                  <span
                    className={
                      log.tone === 'cmd'
                        ? 'font-bold text-white'
                        : log.tone === 'error'
                        ? 'text-red-400'
                        : log.tone === 'warn'
                        ? 'text-amber-300'
                        : 'text-slate-300'
                    }
                  >
                    {log.text}
                  </span>
                </div>
              ))}
            </div>

            {/* Input Bar */}
            <form onSubmit={sendConsoleCommand} className="mt-4 flex items-center gap-2 border-t border-white/10 pt-4">
              <span className="font-mono text-xs font-bold text-brand">txAdmin@flovmp:~$</span>
              <input
                value={consoleInput}
                onChange={(e) => setConsoleInput(e.target.value)}
                placeholder="broadcast [текст], restart, stop или команда сервера..."
                className="flex-1 bg-transparent font-mono text-xs text-white placeholder-slate-600 focus:outline-none"
              />
              <button type="submit" className="btn btn-primary h-8 px-4 text-xs">
                <Send className="h-3.5 w-3.5" />
                Отправить
              </button>
            </form>
          </div>
        </div>
      )}

      {/* ============ AI TROUBLESHOOTER ============ */}
      {tab === 'troubleshoot' && (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <div className="flex items-center gap-2">
                <Zap className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">FloV:AI Диагностика и Автоисправление сбоев</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                Анализ логов запуска сервера, сбоев CoreCLR (.NET), портов UDP 7788, BattlEye и resource.toml
              </p>
            </div>
          </div>

          {/* Quick presets */}
          <div>
            <div className="mb-2 text-xs font-semibold text-slate-400">Пресеты типовых сбоев для быстрой проверки:</div>
            <div className="flex flex-wrap gap-2">
              {[
                {
                  name: 'Сбой CoreCLR (.NET)',
                  text: 'FATAL [CoreCLR] System.IO.FileNotFoundException: Could not load file or assembly FloVMP.Gamemode.dll',
                },
                {
                  name: 'Блокировка UDP 7788',
                  text: 'ERROR [Network] Failed to bind UDP socket on 0.0.0.0:7788: Address already in use / Firewall block',
                },
                {
                  name: 'Отсутствие .bin файлов',
                  text: 'ERROR [Server] Failed to load altv data file: data/release/data/vehicles.bin missing or corrupted',
                },
                {
                  name: 'Ошибка resource.toml',
                  text: 'ERROR [Resource] Failed to parse resource.toml: Invalid TOML syntax at line 14',
                },
              ].map((sample) => (
                <button
                  key={sample.name}
                  onClick={() => {
                    setTroubleshootText(sample.text);
                    void runTroubleshoot(sample.text);
                  }}
                  className="rounded-xl border border-white/10 bg-white/[0.03] px-3 py-1.5 font-mono text-[11px] text-slate-300 transition hover:border-brand/40 hover:bg-brand/10 hover:text-brand"
                >
                  {sample.name}
                </button>
              ))}
            </div>
          </div>

          {/* Input Area */}
          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <FieldLabel>Фрагмент лога ошибки altv-server или исключения C#</FieldLabel>
            <textarea
              rows={5}
              value={troubleshootText}
              onChange={(e) => setTroubleshootText(e.target.value)}
              placeholder="Вставьте сюда текст ошибки (например: FATAL [CoreCLR] System.Exception: ...)"
              className="field w-full p-4 font-mono text-xs text-slate-200"
            />
            <div className="mt-4 flex justify-end">
              <button
                onClick={() => runTroubleshoot()}
                disabled={diagnosing || !troubleshootText.trim()}
                className="btn btn-primary h-10 px-6 text-xs disabled:opacity-50"
              >
                {diagnosing ? <Spinner className="h-4 w-4" /> : <Zap className="h-4 w-4" />}
                {diagnosing ? 'Нейроанализ логов…' : 'Диагностировать ошибку'}
              </button>
            </div>
          </div>

          {/* Diagnosis Result */}
          {diagnosticResult && (
            <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8 animate-fade-in space-y-6">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className={`flex h-10 w-10 items-center justify-center rounded-xl font-bold ${
                      diagnosticResult.severity === 'CRITICAL'
                        ? 'bg-red-500/20 text-red-400'
                        : 'bg-amber-500/20 text-amber-400'
                    }`}
                  >
                    <AlertTriangle className="h-5 w-5" />
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-white">{diagnosticResult.title}</h3>
                    <div className="font-mono text-xs text-slate-400">Категория: {diagnosticResult.category}</div>
                  </div>
                </div>
                <Badge tone={diagnosticResult.severity === 'CRITICAL' ? 'red' : 'amber'}>
                  {diagnosticResult.severity}
                </Badge>
              </div>

              <div className="rounded-2xl border border-white/10 bg-ink-950/60 p-4">
                <div className="text-xs font-semibold text-slate-400 mb-1">Причина инцидента:</div>
                <p className="text-sm text-slate-200 leading-relaxed">{diagnosticResult.explanation}</p>
              </div>

              <div>
                <div className="text-xs font-semibold text-brand mb-3 uppercase tracking-wider font-mono">
                  Пошаговое решение проблемы:
                </div>
                <div className="space-y-3">
                  {diagnosticResult.actionableFixes.map((step: string, idx: number) => (
                    <div
                      key={idx}
                      className="flex items-start gap-3 rounded-xl border border-white/[0.06] bg-white/[0.02] p-3 text-xs"
                    >
                      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-brand/10 font-mono font-bold text-brand">
                        {idx + 1}
                      </span>
                      <div className="text-slate-300 font-mono leading-relaxed pt-0.5">{step}</div>
                    </div>
                  ))}
                </div>
              </div>

              {diagnosticResult.docsReference && (
                <div className="pt-2 font-mono text-xs text-slate-400">
                  Документация:{' '}
                  <span className="text-cyber underline cursor-pointer">{diagnosticResult.docsReference}</span>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* ============ OVERVIEW ============ */}
      {tab === 'overview' && (
        <div className="relative space-y-8 animate-fade-in">
          {/* Onboarding */}
          <div className="glass card-edge rounded-3xl p-6">
            <div className="mb-4 flex items-center gap-2">
              <Rocket className="h-4 w-4 text-brand" />
              <h3 className="font-mono text-[11px] font-bold uppercase tracking-widest text-brand">
                Быстрый старт проекта
              </h3>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {onboarding.map((s) => (
                <div
                  key={s.title}
                  className="flex items-center gap-3 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4"
                >
                  {s.done ? (
                    <Check className="h-5 w-5 shrink-0 text-emeraldx" />
                  ) : (
                    <CircleDot className={`h-5 w-5 shrink-0 ${s.warn ? 'text-amber-400' : 'text-slate-500'}`} />
                  )}
                  <div className="min-w-0">
                    <div className="text-xs font-bold text-white">{s.title}</div>
                    <div className="truncate text-[11px] text-slate-400">{s.note}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Licenses */}
          <div>
            <div className="mb-5 flex items-end justify-between">
              <div>
                <h2 className="flex items-center gap-2 text-lg font-black text-white">
                  <KeyRound className="h-5 w-5 text-brand" />
                  Ваши лицензии
                </h2>
                <p className="mt-1 text-xs text-slate-400">
                  Каждый ключ привязывается к IPv4 игрового сервера
                </p>
              </div>
              <span className="font-mono text-xs text-slate-500">Всего: {licenses.length}</span>
            </div>

            {licenses.length === 0 ? (
              <div className="glass card-edge rounded-3xl p-12 text-center">
                <KeyRound className="mx-auto h-10 w-10 text-slate-600" />
                <p className="mt-3 font-medium text-slate-300">У вас пока нет активных лицензий</p>
                <button onClick={() => setNewLicOpen(true)} className="btn btn-primary mt-4 h-10 px-4 text-xs">
                  Создать первую лицензию
                </button>
              </div>
            ) : (
              <div className="space-y-5">
                {licenses.map((lic) => {
                  const expired = new Date(lic.expires_at).getTime() < Date.now();
                  const active = lic.is_active && !expired;
                  const revealed = showKeyId === lic.id;
                  return (
                    <div key={lic.id} className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-7">
                      <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                        <div className="min-w-0 flex-1 space-y-3">
                          <div className="flex flex-wrap items-center gap-2.5">
                            <h3 className="text-[17px] font-bold text-white">{lic.server_name}</h3>
                            <Badge tone={planTone(lic.plan)}>Тариф: {lic.plan}</Badge>
                            <span
                              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase ${
                                active
                                  ? 'border-emeraldx/30 bg-emeraldx/15 text-emeraldx'
                                  : 'border-red-500/30 bg-red-500/15 text-red-400'
                              }`}
                            >
                              <span className={`h-1.5 w-1.5 rounded-full ${active ? 'bg-emeraldx animate-pulse' : 'bg-red-400'}`} />
                              {active ? 'Активна' : 'Приостановлена'}
                            </span>
                          </div>

                          {/* key */}
                          <div className="flex max-w-xl items-center gap-2">
                            <div className="flex flex-1 items-center justify-between gap-2 rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2.5 font-mono text-sm text-slate-200">
                              <span className="tracking-wider">
                                {revealed ? lic.license_key : `${lic.license_key.slice(0, 4)}-••••-••••-••••`}
                              </span>
                              <button
                                onClick={() => setShowKeyId(revealed ? null : lic.id)}
                                className="text-slate-500 transition hover:text-white"
                                title={revealed ? 'Скрыть ключ' : 'Показать ключ'}
                              >
                                {revealed ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                              </button>
                            </div>
                            <button
                              onClick={() => copy(lic.license_key)}
                              className="btn btn-ghost h-[42px] w-[42px] shrink-0 p-0"
                              title="Скопировать ключ"
                            >
                              {copied === lic.license_key ? (
                                <Check className="h-4 w-4 text-emeraldx" />
                              ) : (
                                <Copy className="h-4 w-4" />
                              )}
                            </button>
                          </div>

                          <div className="flex flex-wrap items-center gap-x-5 gap-y-2 pt-1 font-mono text-[11px] text-slate-400">
                            <span className="flex items-center gap-1.5">
                              <Globe className="h-3.5 w-3.5 text-brand" />
                              IP: <strong className="text-white">{lic.bound_ip === '0.0.0.0' ? 'любой (0.0.0.0)' : lic.bound_ip}</strong>
                            </span>
                            <span className="flex items-center gap-1.5">
                              <Server className="h-3.5 w-3.5 text-cyber" />
                              Слоты: <strong className="text-white">{lic.max_players}</strong>
                            </span>
                            <span className="flex items-center gap-1.5">
                              <ShieldCheck className="h-3.5 w-3.5 text-violetx" />
                              Истекает: <strong className="text-white">{dateShort(lic.expires_at)}</strong>
                            </span>
                          </div>
                        </div>

                        <div className="flex shrink-0 gap-2.5">
                          <button onClick={() => openIpModal(lic)} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                            <Settings2 className="h-4 w-4 text-brand" />
                            Настроить IP
                          </button>
                          <a
                            href="/cdn/FloVMP-Server-x64-Linux.tar.gz"
                            download
                            className="btn btn-ghost h-10 px-3.5 text-xs font-semibold"
                          >
                            <Download className="h-4 w-4 text-emeraldx" />
                            Сервер
                          </a>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* server.toml guide */}
          <div className="glass card-edge rounded-3xl p-6 sm:p-8">
            <h3 className="flex items-center gap-2 text-base font-bold text-white">
              <Terminal className="h-5 w-5 text-brand" />
              Прописка ключа в <code className="rounded bg-white/5 px-1.5 py-0.5 font-mono text-xs text-brand">server.toml</code>
            </h3>
            <ol className="mt-4 space-y-3 text-[13px] text-slate-300">
              <li>
                <strong className="text-white">1.</strong> Скачайте архив движка кнопкой «Сервер» на карточке лицензии.
              </li>
              <li>
                <strong className="text-white">2.</strong> В файле <code className="font-mono text-xs text-brand">server.toml</code> укажите блок:
                <pre className="mt-2 overflow-x-auto rounded-xl bg-ink-950/70 p-3 font-mono text-[12px] text-slate-200">
{`[licensing]
key      = "${primaryLic?.license_key || 'FLV-XXXX-XXXX-XXXX'}"
bound_ip = "${isIpBound ? primaryLic!.bound_ip : '188.127.229.224'}"`}
                </pre>
              </li>
              <li>
                <strong className="text-white">3.</strong> Запустите <code className="font-mono text-xs text-emeraldx">./start.sh</code> — узел пройдёт онлайн-верификацию и начнёт приём игроков.
              </li>
            </ol>
          </div>
        </div>
      )}

      {/* ============ TELEMETRY ============ */}
      {tab === 'telemetry' && (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-5 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-white">
                <Activity className="h-5 w-5 text-brand" />
                Мониторинг игрового узла
              </h2>
              <p className="mt-1 font-mono text-[11px] text-slate-400">
                key: <span className="text-brand">{primaryLic?.license_key || '—'}</span> · VDS:{' '}
                <span className="text-white">{primaryLic?.bound_ip || '188.127.229.224'}</span>
              </p>
            </div>
            <div className="flex gap-2.5">
              <button onClick={loadTelemetry} disabled={loadingTelemetry} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                <RefreshCw className={`h-4 w-4 ${loadingTelemetry ? 'animate-spin text-brand' : ''}`} />
                Обновить
              </button>
              <button
                onClick={sendHeartbeat}
                disabled={sendingHb || !primaryLic}
                className="btn h-10 border border-brand/40 bg-brand/15 px-3.5 text-xs font-bold text-brand transition hover:bg-brand/25 disabled:opacity-50"
              >
                {sendingHb ? <Spinner className="h-4 w-4" /> : <Zap className="h-4 w-4" />}
                Тестовый heartbeat
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard icon={Cpu} tone="text-emeraldx" ring="border-emeraldx/40 bg-emeraldx/10" label="Tick Rate" value={`${latest?.tick_rate ?? 60}.0`} unit="Hz" foot="Синхронизация 16.6 ms" />
            <MetricCard icon={Gauge} tone="text-brand" ring="border-brand/40 bg-brand/10" label="Server FPS" value={`${latest?.fps ?? 60}.0`} unit="FPS" foot="Физика без просадок" />
            <MetricCard icon={HardDrive} tone="text-cyber" ring="border-cyber/40 bg-cyber/10" label="CoreCLR RAM" value={`${latest?.memory_mb ?? 248}`} unit="MB" foot="ОЗУ оптимизировано" />
            <MetricCard icon={Users} tone="text-violetx" ring="border-violetx/40 bg-violetx/10" label="Игроки онлайн" value={`${latest?.players ?? 1}`} unit={`/ ${primaryLic?.max_players ?? 1500}`} foot="Слоты активны" />
          </div>

          {/* 24-Hour Activity Chart & SLA */}
          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h3 className="flex items-center gap-2 text-base font-bold text-white">
                  <Activity className="h-5 w-5 text-brand" />
                  Суточная динамика онлайна игроков (24h Activity)
                </h3>
                <p className="mt-0.5 text-xs text-slate-400">
                  Почасовая активность серверов проекта с расчётом пикового онлайна и аптайма SLA
                </p>
              </div>
              <div className="flex items-center gap-3 font-mono text-xs">
                <span className="flex items-center gap-1.5 rounded-full border border-emeraldx/30 bg-emeraldx/15 px-3 py-1 text-emeraldx font-bold">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  SLA: 99.98%
                </span>
                <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-slate-300">
                  Пик: <strong className="text-brand">184 игрока</strong>
                </span>
              </div>
            </div>

            {/* SVG Activity Curve */}
            <div className="h-44 w-full">
              <svg className="h-full w-full overflow-visible" viewBox="0 0 800 160" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="curveGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#ff3d8a" stopOpacity="0.4" />
                    <stop offset="100%" stopColor="#ff3d8a" stopOpacity="0.0" />
                  </linearGradient>
                </defs>
                {/* Grid Lines */}
                <line x1="0" y1="40" x2="800" y2="40" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />
                <line x1="0" y1="80" x2="800" y2="80" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />
                <line x1="0" y1="120" x2="800" y2="120" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />

                {/* Filled Area */}
                <path
                  d="M0,130 C70,120 120,140 180,110 C240,70 300,90 360,60 C420,30 480,45 540,25 C600,10 660,35 720,20 C760,10 790,25 800,30 L800,160 L0,160 Z"
                  fill="url(#curveGradient)"
                />
                {/* Stroke Line */}
                <path
                  d="M0,130 C70,120 120,140 180,110 C240,70 300,90 360,60 C420,30 480,45 540,25 C600,10 660,35 720,20 C760,10 790,25 800,30"
                  fill="none"
                  stroke="#ff3d8a"
                  strokeWidth="3"
                  strokeLinecap="round"
                />

                {/* Peak Dot */}
                <circle cx="600" cy="10" r="5" fill="#ff3d8a" className="animate-pulse" />
                <circle cx="600" cy="10" r="10" fill="none" stroke="#ff3d8a" strokeWidth="1.5" strokeOpacity="0.5" />
              </svg>
            </div>

            {/* Time markers */}
            <div className="mt-3 flex justify-between font-mono text-[10px] text-slate-500">
              <span>00:00 (Ночь)</span>
              <span>04:00</span>
              <span>08:00 (Утро)</span>
              <span>12:00 (День)</span>
              <span>16:00</span>
              <span>20:00 (Прайм-тайм)</span>
              <span>23:59</span>
            </div>
          </div>

          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-white">
              <Activity className="h-4 w-4 text-cyber" />
              История пакетов телеметрии (последние 30)
            </h3>
            {telemetry.length === 0 ? (
              <p className="py-10 text-center text-xs text-slate-500">
                Нет записанных пакетов. Нажмите «Тестовый heartbeat», чтобы отправить пакет со стенда.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-left font-mono text-xs">
                  <thead>
                    <tr className="border-b border-white/[0.08] uppercase tracking-wider text-slate-500">
                      <th className="pb-3 pr-3 font-semibold">Время</th>
                      <th className="pb-3 pr-3 font-semibold">IP узла</th>
                      <th className="pb-3 pr-3 font-semibold">Онлайн</th>
                      <th className="pb-3 pr-3 font-semibold">Tick</th>
                      <th className="pb-3 pr-3 font-semibold">FPS</th>
                      <th className="pb-3 pr-3 font-semibold">ОЗУ</th>
                      <th className="pb-3 pr-3 font-semibold">Статус</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/[0.05] text-slate-300">
                    {[...telemetry].reverse().map((pt) => (
                      <tr key={pt.id} className="transition-colors hover:bg-white/[0.03]">
                        <td className="py-2.5 pr-3 text-white">{timeShort(pt.recorded_at)}</td>
                        <td className="py-2.5 pr-3 text-slate-400">{pt.server_ip}</td>
                        <td className="py-2.5 pr-3 font-bold text-brand">{pt.players} / {pt.max_players}</td>
                        <td className="py-2.5 pr-3 text-emeraldx">{pt.tick_rate} Hz</td>
                        <td className="py-2.5 pr-3">{pt.fps}</td>
                        <td className="py-2.5 pr-3 text-cyber">{pt.memory_mb} MB</td>
                        <td className="py-2.5 pr-3">
                          <span className="rounded bg-emeraldx/15 px-2 py-0.5 text-[10px] text-emeraldx">OK</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ============ DOWNLOADS & SDK ============ */}
      {tab === 'sdk' && (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <div className="flex items-center gap-2">
                <Download className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">Дистрибутивы и SDK FloV:MP</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                Автономный мультиплеерный рантайм, модули CoreCLR, библиотека C# SDK и утилиты FastDL
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
            {[
              {
                title: 'FloV:MP Server Core (Linux)',
                desc: 'Автономный рантайм для VDS Ubuntu 22.04 / Debian 12. Включает coreclr-module, BattlEye и UDP стример.',
                badge: 'Linux x64 · 85 MB',
                badgeTone: 'cyber',
                link: '/cdn/FloVMP-Server-x64-Linux.tar.gz',
                btnText: 'Скачать tar.gz',
              },
              {
                title: 'FloV:MP Server Core (Windows)',
                desc: 'Локальный сервер для разработки под Windows 10/11/Server 2022. Полная изоляция от внешних бэкендов.',
                badge: 'Win64 · 92 MB',
                badgeTone: 'brand',
                link: '/cdn/FloVMP-Server-x64-Windows.zip',
                btnText: 'Скачать zip',
              },
              {
                title: 'FloVMP.Core C# .NET 8 SDK',
                desc: 'Пакет C# API: EntityStreamer, RemoteServerAgent, FactionEngine, LicensingService для создания гейммодов.',
                badge: 'NuGet / DLL · 14 MB',
                badgeTone: 'emeraldx',
                link: '/cdn/FloVMP-SDK-v1.0.4.zip',
                btnText: 'Скачать SDK',
              },
              {
                title: 'FloV:MP Asset Packer (CLI)',
                desc: 'Консольная утилита шифрования клиентских ресурсов и автогенерации манифестов FastDL перед релизом.',
                badge: 'CLI Tool · 8 MB',
                badgeTone: 'violetx',
                link: '/cdn/flovmp-packer.exe',
                btnText: 'Скачать Packer',
              },
              {
                title: 'Electron Launcher Template',
                desc: 'Исходный код брендированного лаунчера на Chromium UI + C# Native Bridge с аппаратным ускорением.',
                badge: 'Source · 24 MB',
                badgeTone: 'cyber',
                link: '/cdn/FloVMP-Launcher-Template.zip',
                btnText: 'Скачать шаблон',
              },
              {
                title: 'Примеры гейммодов (Templates)',
                desc: 'Готовые шаблоны ролевых проектов: RP Основа, Дрифт-сервер, DM Арена с полной C# типизацией.',
                badge: 'Samples · 5 MB',
                badgeTone: 'brand',
                link: '/cdn/FloVMP-Gamemode-Samples.zip',
                btnText: 'Скачать примеры',
              },
            ].map((item) => (
              <div
                key={item.title}
                className="glass-panel card-edge flex flex-col justify-between rounded-3xl p-6 shadow-glass"
              >
                <div>
                  <div className="flex items-center justify-between">
                    <Badge tone={item.badgeTone as any}>{item.badge}</Badge>
                    <Download className="h-4 w-4 text-slate-500" />
                  </div>
                  <h3 className="mt-4 text-base font-bold text-white">{item.title}</h3>
                  <p className="mt-2 text-xs leading-relaxed text-slate-400">{item.desc}</p>
                </div>
                <div className="mt-6 border-t border-white/[0.08] pt-4">
                  <a
                    href={item.link}
                    download
                    className="btn btn-ghost h-10 w-full text-xs font-semibold text-brand hover:bg-brand/10"
                  >
                    <Download className="h-4 w-4" />
                    {item.btnText}
                  </a>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ============ BUILDER ============ */}
      {tab === 'builder' && (
        <div className="relative grid grid-cols-1 gap-8 animate-fade-in lg:grid-cols-2">
          <div className="glass-panel card-edge rounded-3xl p-7 shadow-glass sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl border border-cyber/30 bg-cyber/10 text-cyber">
                <Layers className="h-5 w-5" />
              </span>
              <div>
                <h2 className="text-lg font-bold text-white">Сборщик кастомного лаунчера</h2>
                <p className="text-xs text-slate-400">Компиляция установщика под бренд и IP проекта</p>
              </div>
            </div>

            <form onSubmit={buildLauncher} className="mt-6 space-y-5">
              <div>
                <FieldLabel>Название проекта</FieldLabel>
                <input
                  required
                  value={bProject}
                  onChange={(e) => setBProject(e.target.value)}
                  placeholder="Держава Онлайн"
                  className="field h-11 px-4"
                />
              </div>

              <div>
                <FieldLabel>Фирменный HEX-цвет</FieldLabel>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={bColor}
                    onChange={(e) => setBColor(e.target.value)}
                    className="h-11 w-14 cursor-pointer rounded-xl border border-white/10 bg-ink-950/60"
                  />
                  <input
                    value={bColor}
                    onChange={(e) => setBColor(e.target.value)}
                    className="field h-11 flex-1 px-4 font-mono"
                  />
                </div>
                <div className="mt-2.5 flex flex-wrap gap-1.5">
                  {COLOR_PRESETS.map((p) => (
                    <button
                      key={p.hex}
                      type="button"
                      onClick={() => setBColor(p.hex)}
                      className={`flex items-center gap-1.5 rounded-lg border px-2 py-1 text-[10px] font-semibold transition ${
                        bColor.toLowerCase() === p.hex.toLowerCase()
                          ? 'border-white/30 bg-white/10 text-white'
                          : 'border-white/10 text-slate-400 hover:border-white/20'
                      }`}
                    >
                      <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: p.hex }} />
                      {p.name}
                    </button>
                  ))}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <FieldLabel>IP сервера</FieldLabel>
                  <input required value={bIp} onChange={(e) => setBIp(e.target.value)} className="field h-11 px-4 font-mono" />
                </div>
                <div>
                  <FieldLabel>UDP порт</FieldLabel>
                  <input required value={bPort} onChange={(e) => setBPort(e.target.value)} className="field h-11 px-4 font-mono" />
                </div>
              </div>

              <button
                type="submit"
                disabled={building || licenses.length === 0}
                className="btn btn-primary h-11 w-full text-sm disabled:opacity-50"
              >
                {building ? <Spinner className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}
                {building ? 'Компиляция…' : 'Скомпилировать лаунчер проекта'}
              </button>
            </form>
          </div>

          <div className="glass-panel card-edge flex flex-col rounded-3xl p-7 shadow-glass sm:p-8">
            <h3 className="flex items-center gap-2 text-base font-bold text-white">
              <Rocket className="h-4 w-4 text-brand" />
              Статус сборщика
            </h3>

            {building || buildStage > 0 ? (
              <div className="mt-5 space-y-2.5">
                {BUILD_STAGES.map((s, i) => {
                  const state = buildStage > i ? 'done' : buildStage === i ? 'active' : 'idle';
                  return (
                    <div
                      key={s}
                      className={`flex items-center gap-3 rounded-xl border px-4 py-2.5 text-xs transition-all ${
                        state === 'done'
                          ? 'border-emeraldx/20 bg-emeraldx/[0.06] text-emeraldx'
                          : state === 'active'
                          ? 'border-brand/30 bg-brand/[0.08] text-white'
                          : 'border-white/[0.06] bg-white/[0.02] text-slate-500'
                      }`}
                    >
                      {state === 'done' ? (
                        <Check className="h-4 w-4" />
                      ) : state === 'active' ? (
                        <Spinner className="h-4 w-4" />
                      ) : (
                        <CircleDot className="h-4 w-4" />
                      )}
                      {s}
                    </div>
                  );
                })}
              </div>
            ) : null}

            {buildResult ? (
              <div className="mt-5 space-y-4 rounded-2xl border border-emeraldx/25 bg-emeraldx/[0.06] p-5 text-xs animate-fade-in">
                <div className="flex items-center gap-2 font-bold text-emeraldx">
                  <Check className="h-5 w-5" />
                  Лаунчер собран (Build #{buildResult.buildId})
                </div>
                <div className="space-y-1.5 font-mono text-slate-300">
                  <div>Проект: <strong className="text-white">{buildResult.projectName}</strong></div>
                  <div className="flex items-center gap-1.5">
                    Цвет:
                    <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: buildResult.primaryColor }} />
                    <span style={{ color: buildResult.primaryColor }}>{buildResult.primaryColor}</span>
                  </div>
                  <div>Эндпоинт: <strong className="text-white">{buildResult.config.serverIp}:{buildResult.config.serverPort}</strong></div>
                  <div>Лицензия: <strong className="text-white">{buildResult.config.licenseKey}</strong></div>
                </div>
                <a href={buildResult.downloadUrl} download className="btn h-10 w-full bg-emeraldx text-xs font-bold text-ink-950 transition hover:brightness-110">
                  <Download className="h-4 w-4" />
                  Скачать {buildResult.projectName}-Setup.exe
                </a>
              </div>
            ) : !building && buildStage === 0 ? (
              <div className="mt-5 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-8 text-center text-xs text-slate-500">
                <Layers className="mx-auto h-10 w-10 text-slate-600" />
                <p className="mt-3 font-semibold text-slate-300">Ожидание запуска сборки</p>
                <p className="mt-1">
                  Укажите параметры слева и нажмите «Скомпилировать». Билдер упакует Electron + C#
                  Native Bridge и FastDL-манифест.
                </p>
              </div>
            ) : null}

            <div className="mt-6 space-y-2 border-t border-white/[0.08] pt-5 font-mono text-[11px] text-slate-400">
              {[
                'Аппаратное ускорение Chromium UI (blur / shadows)',
                'Прямой коннектор C# .NET 8 (FloVMP.Connect)',
                'Вшитый FastDL-кэш с CDN VDS',
              ].map((x) => (
                <div key={x} className="flex items-center gap-2">
                  <Check className="h-3.5 w-3.5 text-brand" />
                  {x}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ============ BILLING ============ */}
      {tab === 'billing' && (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-5 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-white">
                <CreditCard className="h-5 w-5 text-brand" />
                Счета и управление подпиской
              </h2>
              <p className="mt-1 text-xs text-slate-400">
                Счета формируются автоматически, продление лицензий — моментальное
              </p>
            </div>
            <div className="flex gap-2.5">
              <button onClick={loadInvoices} disabled={loadingInvoices} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                <RefreshCw className={`h-4 w-4 ${loadingInvoices ? 'animate-spin text-brand' : ''}`} />
                Обновить
              </button>
              <button onClick={() => setInvoiceOpen(true)} className="btn btn-primary h-10 px-4 text-xs">
                <Plus className="h-4 w-4" />
                Выставить счёт на продление
              </button>
            </div>
          </div>

          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-white">
              <CreditCard className="h-4 w-4 text-violetx" />
              История выставленных счетов
            </h3>
            {invoices.length === 0 ? (
              <div className="py-10 text-center text-xs text-slate-500">
                <p>Счетов пока нет.</p>
                <button onClick={() => setInvoiceOpen(true)} className="btn btn-primary mt-3 h-9 px-4 text-xs">
                  Выставить счёт
                </button>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-left text-xs">
                  <thead>
                    <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                      <th className="pb-3 pr-3 font-semibold">№</th>
                      <th className="pb-3 pr-3 font-semibold">Тариф</th>
                      <th className="pb-3 pr-3 font-semibold">Сумма</th>
                      <th className="pb-3 pr-3 font-semibold">Метод</th>
                      <th className="pb-3 pr-3 font-semibold">Создан</th>
                      <th className="pb-3 pr-3 font-semibold">Статус</th>
                      <th className="pb-3 pr-3 text-right font-semibold">Действие</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/[0.05] text-slate-300">
                    {invoices.map((inv) => {
                      const paid = inv.status === 'paid';
                      return (
                        <tr key={inv.id} className="transition-colors hover:bg-white/[0.03]">
                          <td className="py-3 pr-3 font-mono font-bold text-white">#{inv.id}</td>
                          <td className="py-3 pr-3 font-mono uppercase text-brand">{inv.plan}</td>
                          <td className="py-3 pr-3 font-mono text-sm font-bold text-white">
                            {Number(inv.amount_rub).toLocaleString('ru-RU')} ₽
                          </td>
                          <td className="py-3 pr-3 font-mono capitalize text-slate-400">{inv.payment_method}</td>
                          <td className="py-3 pr-3 font-mono text-slate-400">{dateShort(inv.created_at)}</td>
                          <td className="py-3 pr-3">
                            <Badge tone={paid ? 'emerald' : 'amber'}>{paid ? 'Оплачен' : 'Ожидает оплаты'}</Badge>
                          </td>
                          <td className="py-3 pr-3 text-right">
                            {paid ? (
                              <span className="text-[11px] text-slate-600">Закрыт</span>
                            ) : (
                              <button
                                onClick={() => payInvoice(inv.id)}
                                disabled={payingId === inv.id}
                                className="btn h-8 bg-emeraldx px-3 text-[11px] font-bold text-ink-950 transition hover:brightness-110 disabled:opacity-50"
                              >
                                {payingId === inv.id ? <Spinner className="h-3.5 w-3.5" /> : 'Оплатить'}
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ============ AFFILIATE ============ */}
      {tab === 'affiliate' && (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge rounded-3xl p-7 shadow-glass sm:p-8">
            <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
              <div className="max-w-2xl space-y-2">
                <span className="eyebrow text-brand flex items-center gap-2">
                  <Percent className="h-4 w-4" />
                  Партнёрская программа
                </span>
                <h3 className="text-2xl font-black text-white">Зарабатывайте 20% от оплат серверов</h3>
                <p className="text-xs leading-relaxed text-slate-400">
                  Рекомендуйте FloV:MP владельцам GTA V RP-серверов. Вы получаете{' '}
                  <strong className="text-white">20% пожизненно</strong> с каждого продления лицензии
                  приглашённого проекта. Приглашённый проект получает скидку{' '}
                  <strong className="text-emeraldx">10%</strong>.
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <div className="rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2.5 font-mono text-sm font-bold text-brand">
                  {promoCode}
                </div>
                <button
                  onClick={() => copy(promoCode)}
                  className="btn h-11 border border-brand/40 bg-brand/15 px-4 text-xs font-bold text-brand transition hover:bg-brand/25"
                >
                  {copied === promoCode ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
                  {copied === promoCode ? 'Скопировано' : 'Копировать'}
                </button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-3">
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">Приглашено проектов</div>
              <div className="mt-2 font-mono text-3xl font-black text-white">0</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">Активные рефералы</div>
            </div>
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">Начислено вознаграждений</div>
              <div className="mt-2 font-mono text-3xl font-black text-emeraldx">0 ₽</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">Доступно к выводу</div>
            </div>
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">Процент отчислений</div>
              <div className="mt-2 font-mono text-3xl font-black text-brand">20%</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">Пожизненно со всех платежей</div>
            </div>
          </div>
        </div>
      )}

      {/* ============ MODALS ============ */}
      <Modal
        open={!!ipLicense}
        onClose={() => setIpLicense(null)}
        title="Привязка IP-адреса сервера"
        description="Публичный IPv4 адрес VDS, на котором развёрнут игровой сервер"
        maxWidth="max-w-md"
      >
        {ipErr && (
          <div className="mb-4 rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2.5 text-xs text-red-400">
            {ipErr}
          </div>
        )}
        <form onSubmit={saveIp} className="space-y-4">
          <div>
            <FieldLabel>Название сервера</FieldLabel>
            <input required value={ipName} onChange={(e) => setIpName(e.target.value)} className="field h-11 px-4" />
          </div>
          <div>
            <FieldLabel>IPv4 адрес</FieldLabel>
            <input
              required
              value={ipValue}
              onChange={(e) => setIpValue(e.target.value)}
              placeholder="188.127.229.224"
              className="field h-11 px-4 font-mono"
            />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setIpLicense(null)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              Отмена
            </button>
            <button type="submit" disabled={savingIp} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {savingIp ? <Spinner className="h-4 w-4" /> : null}
              Сохранить привязку
            </button>
          </div>
        </form>
      </Modal>

      <Modal
        open={newLicOpen}
        onClose={() => setNewLicOpen(false)}
        title="Оформление новой лицензии"
        description="Выберите тариф под масштабы вашего игрового проекта"
      >
        <form onSubmit={createLicense} className="space-y-5">
          <div className="grid grid-cols-3 gap-3">
            {[
              { id: 'indie', name: 'Инди', slots: '128 слотов', price: 'Бесплатно', tone: 'slate' },
              { id: 'business', name: 'RP Проект', slots: '512 слотов', price: '14 900 ₽', tone: 'brand' },
              { id: 'enterprise', name: 'Enterprise', slots: '1500+ слотов', price: '49 000 ₽', tone: 'cyber' },
            ].map((p) => (
              <button
                type="button"
                key={p.id}
                onClick={() => setNewPlan(p.id)}
                className={`rounded-2xl border p-4 text-center transition-all ${
                  newPlan === p.id
                    ? p.tone === 'cyber'
                      ? 'border-cyber/60 bg-cyber/10'
                      : p.tone === 'brand'
                      ? 'border-brand/60 bg-brand/10 shadow-neon-pink'
                      : 'border-white/30 bg-white/10'
                    : 'border-white/10 bg-white/[0.02]'
                }`}
              >
                <div className="text-xs font-bold text-white">{p.name}</div>
                <div className="mt-1 text-[10px] text-slate-400">{p.slots}</div>
                <div className="mt-2 text-xs font-bold text-white">{p.price}</div>
              </button>
            ))}
          </div>
          <div>
            <FieldLabel>Название вашего сервера</FieldLabel>
            <input
              required
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="Moscow Night RP"
              className="field h-11 px-4"
            />
          </div>
          <div>
            <FieldLabel>IP сервера (можно указать позже)</FieldLabel>
            <input value={newIp} onChange={(e) => setNewIp(e.target.value)} placeholder="0.0.0.0" className="field h-11 px-4 font-mono" />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setNewLicOpen(false)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              Отмена
            </button>
            <button type="submit" disabled={creatingLic} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {creatingLic ? <Spinner className="h-4 w-4" /> : null}
              Активировать лицензию
            </button>
          </div>
        </form>
      </Modal>

      <Modal
        open={newProjOpen}
        onClose={() => setNewProjOpen(false)}
        title="Создание нового проекта"
        description="Проект объединяет единую лицензию и несколько сред: Production, Development, Test"
      >
        <form onSubmit={createProjectHandler} className="space-y-5">
          <div className="grid grid-cols-2 gap-3">
            {[
              { id: 'business', name: 'RP Проект', slots: '512 слотов', tone: 'brand' },
              { id: 'enterprise', name: 'Enterprise', slots: '1500+ слотов', tone: 'cyber' },
            ].map((p) => (
              <button
                type="button"
                key={p.id}
                onClick={() => setNewProjPlan(p.id)}
                className={`rounded-2xl border p-4 text-center transition-all ${
                  newProjPlan === p.id
                    ? p.tone === 'cyber'
                      ? 'border-cyber/60 bg-cyber/10'
                      : 'border-brand/60 bg-brand/10 shadow-neon-pink'
                    : 'border-white/10 bg-white/[0.02]'
                }`}
              >
                <div className="text-xs font-bold text-white">{p.name}</div>
                <div className="mt-1 text-[10px] text-slate-400">{p.slots}</div>
              </button>
            ))}
          </div>
          <div>
            <FieldLabel>Название проекта</FieldLabel>
            <input
              required
              value={newProjName}
              onChange={(e) => {
                setNewProjName(e.target.value);
                if (!newProjSlug || newProjSlug === newProjName.toLowerCase().replace(/\s+/g, '-')) {
                  setNewProjSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '-'));
                }
              }}
              placeholder="Florida V RolePlay"
              className="field h-11 px-4"
            />
          </div>
          <div>
            <FieldLabel>Slug идентификатор (URL проекта)</FieldLabel>
            <input
              required
              value={newProjSlug}
              onChange={(e) => setNewProjSlug(e.target.value)}
              placeholder="florida-v"
              className="field h-11 px-4 font-mono"
            />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button
              type="button"
              onClick={() => setNewProjOpen(false)}
              className="px-4 py-2 text-xs text-slate-400 transition hover:text-white"
            >
              Отмена
            </button>
            <button
              type="submit"
              disabled={creatingProj}
              className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50"
            >
              {creatingProj ? <Spinner className="h-4 w-4" /> : null}
              Создать проект
            </button>
          </div>
        </form>
      </Modal>

      {/* Project Settings & Webhook Modal */}
      <Modal
        open={settingsModalOpen}
        onClose={() => setSettingsModalOpen(false)}
        title="Настройки проекта & Webhooks"
        description="Политика безопасности FloV:ID, пропуск обходников HWID и алерты в Discord / Telegram"
        maxWidth="max-w-2xl"
      >
        <form onSubmit={saveProjectSettings} className="space-y-6">
          {/* HWID Policy Selection */}
          <div>
            <FieldLabel>Политика безопасности FloV:ID & HWID</FieldLabel>
            <div className="grid grid-cols-3 gap-2.5">
              {[
                {
                  id: 'strict',
                  title: 'Строгий (Strict)',
                  desc: 'Блокировка забаненных HWID',
                  tone: 'red',
                },
                {
                  id: 'lenient',
                  title: 'Мягкий (Lenient)',
                  desc: 'Вход разрешён с аудитом',
                  tone: 'brand',
                },
                {
                  id: 'disabled',
                  title: 'Выключен (Disabled)',
                  desc: 'Пускать всех без ограничений',
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
              <FieldLabel>Максимум аккаунтов на 1 HWID</FieldLabel>
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
                  Разрешать игрокам вход через VPN / Proxy
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
                  Оповещения в Discord и Telegram
                </h4>
                <p className="text-[11px] text-slate-400">
                  Автоматическая отправка уведомлений о падении сервера, сбоях и превышении нагрузки
                </p>
              </div>
              <label className="flex items-center gap-2 text-xs text-slate-300 font-semibold cursor-pointer">
                <input
                  type="checkbox"
                  checked={settingAlertsEnabled}
                  onChange={(e) => setSettingAlertsEnabled(e.target.checked)}
                  className="h-4 w-4 rounded accent-brand"
                />
                Включены
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
                  Тест
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
                    Тест
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
              Отмена
            </button>
            <button
              type="submit"
              disabled={savingSettings}
              className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50"
            >
              {savingSettings ? <Spinner className="h-4 w-4" /> : null}
              Сохранить параметры проекта
            </button>
          </div>
        </form>
      </Modal>

      <Modal
        open={invoiceOpen}
        onClose={() => setInvoiceOpen(false)}
        title="Выставление счёта на оплату"
        description="Выберите тариф, период и способ оплаты"
      >
        <form onSubmit={createInvoice} className="space-y-5">
          <div>
            <FieldLabel>Тарифный план</FieldLabel>
            <div className="grid grid-cols-2 gap-3">
              {[
                { id: 'business', name: 'RP Проект', note: '512 слотов · 14 900 ₽/мес', tone: 'brand' },
                { id: 'enterprise', name: 'Enterprise', note: '1500+ слотов · 49 000 ₽', tone: 'cyber' },
              ].map((p) => (
                <button
                  type="button"
                  key={p.id}
                  onClick={() => setInvPlan(p.id)}
                  className={`rounded-xl border p-3 text-left transition ${
                    invPlan === p.id
                      ? p.tone === 'cyber'
                        ? 'border-cyber/60 bg-cyber/10'
                        : 'border-brand/60 bg-brand/10'
                      : 'border-white/10 bg-white/[0.02]'
                  }`}
                >
                  <div className={`text-xs font-bold ${p.tone === 'cyber' ? 'text-cyber' : 'text-brand'}`}>{p.name}</div>
                  <div className="mt-1 font-mono text-[11px] text-slate-400">{p.note}</div>
                </button>
              ))}
            </div>
          </div>

          <div>
            <FieldLabel>Период оплаты</FieldLabel>
            <div className="grid grid-cols-3 gap-2">
              {(
                [
                  ['monthly', '1 месяц'],
                  ['halfYear', '6 мес · −15%'],
                  ['year', '1 год · −30%'],
                ] as const
              ).map(([id, label]) => (
                <button
                  type="button"
                  key={id}
                  onClick={() => setInvPeriod(id)}
                  className={`rounded-xl border px-3 py-2 font-mono text-[11px] font-bold transition ${
                    invPeriod === id ? 'border-brand/60 bg-brand/10 text-white' : 'border-white/10 text-slate-400'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div>
            <FieldLabel>Способ оплаты</FieldLabel>
            <div className="grid grid-cols-3 gap-2">
              {(
                [
                  ['card', 'Карта'],
                  ['sbp', 'СБП QR'],
                  ['crypto', 'USDT'],
                ] as const
              ).map(([id, label]) => (
                <button
                  type="button"
                  key={id}
                  onClick={() => setInvMethod(id)}
                  className={`rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                    invMethod === id ? 'border-brand/60 bg-brand/10 text-white' : 'border-white/10 text-slate-400'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setInvoiceOpen(false)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              Отмена
            </button>
            <button type="submit" disabled={creatingInvoice} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {creatingInvoice ? <Spinner className="h-4 w-4" /> : <ArrowRight className="h-4 w-4" />}
              Выставить счёт
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

/* --------------------------- sub-components --------------------------- */
function MetricCard({
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
