'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Activity,
  ArrowRight,
  Check,
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
  KeyRound,
  Layers,
  Percent,
  Plus,
  RefreshCw,
  Rocket,
  Server,
  Settings2,
  ShieldCheck,
  Terminal,
  Users,
  Zap,
} from 'lucide-react';
import {
  AuroraBlobs,
  Badge,
  FieldLabel,
  Modal,
  Spinner,
  useToast,
} from '@/components/ui';

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

type TabKey = 'overview' | 'telemetry' | 'builder' | 'billing' | 'affiliate';

const TABS: { key: TabKey; label: string; icon: React.ElementType }[] = [
  { key: 'overview', label: 'Ключи и статус', icon: KeyRound },
  { key: 'telemetry', label: 'Телеметрия VDS', icon: Activity },
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
  const [tab, setTab] = useState<TabKey>('overview');

  const [showKeyId, setShowKeyId] = useState<number | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  /* IP modal */
  const [ipLicense, setIpLicense] = useState<License | null>(null);
  const [ipValue, setIpValue] = useState('');
  const [ipName, setIpName] = useState('');
  const [ipErr, setIpErr] = useState('');
  const [savingIp, setSavingIp] = useState(false);

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
    return () => stageTimers.current.forEach(clearTimeout);
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
    } catch {
      show('Не удалось загрузить данные кабинета', 'error');
    } finally {
      setLoading(false);
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
    <div className="relative mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      <AuroraBlobs />
      {node}

      {/* Header */}
      <div className="relative glass-panel card-edge mb-8 flex flex-col gap-5 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
        <div className="flex items-center gap-4">
          <span className="block h-14 w-14 overflow-hidden rounded-2xl border border-white/15 bg-ink-800 shadow-neon-pink">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo-codex.png" alt="" className="h-full w-full object-cover" />
          </span>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-black text-white sm:text-2xl">Здравствуйте, {user?.username}</h1>
              <Badge tone={user?.role === 'admin' ? 'red' : 'brand'}>
                {user?.role === 'admin' ? 'Администратор' : 'Клиент SaaS'}
              </Badge>
            </div>
            <p className="mt-1 text-xs text-slate-400">
              Лицензии, телеметрия VDS, биллинг и брендированный лаунчер FloV:MP
            </p>
          </div>
        </div>
        <button onClick={() => setNewLicOpen(true)} className="btn btn-primary h-11 px-5 text-xs">
          <Plus className="h-4 w-4" />
          Новая лицензия
        </button>
      </div>

      {/* Tabs */}
      <div className="relative mb-8 flex gap-1.5 overflow-x-auto border-b border-white/[0.08] pb-2 no-scrollbar">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`flex shrink-0 items-center gap-2 rounded-xl px-4 py-2.5 font-mono text-[11px] font-bold uppercase tracking-wider transition-all ${
              tab === t.key
                ? 'bg-brand text-white shadow-neon-pink'
                : 'bg-white/[0.03] text-slate-400 hover:bg-white/[0.06] hover:text-white'
            }`}
          >
            <t.icon className="h-4 w-4" />
            {t.label}
          </button>
        ))}
      </div>

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
