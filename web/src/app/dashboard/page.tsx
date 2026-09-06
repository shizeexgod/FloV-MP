'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  Zap,
  Key,
  Server,
  Globe,
  Download,
  Copy,
  Check,
  Eye,
  EyeOff,
  RefreshCw,
  Plus,
  ShieldCheck,
  Clock,
  Terminal,
  Layers,
  Settings,
  AlertCircle,
  ExternalLink,
  Users,
  CheckCircle2,
  Circle,
  CreditCard,
  History,
  TrendingUp,
  Percent,
  Activity,
  Cpu,
  HardDrive,
  Wifi,
  Sparkles,
  ArrowUpRight,
  Play,
  CheckCircle
} from 'lucide-react';

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
  config: {
    serverIp: string;
    serverPort: number;
    licenseKey: string;
  };
  message: string;
}

export default function DashboardPage() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [licenses, setLicenses] = useState<License[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'overview' | 'builder' | 'telemetry' | 'billing' | 'affiliate'>('overview');

  // Key visibility toggles
  const [showKeyId, setShowKeyId] = useState<number | null>(null);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [copiedPromo, setCopiedPromo] = useState(false);

  // IP binding modal state
  const [editingLicense, setEditingLicense] = useState<License | null>(null);
  const [inputIp, setInputIp] = useState('');
  const [inputServerName, setInputServerName] = useState('');
  const [ipModalError, setIpModalError] = useState('');
  const [ipModalSuccess, setIpModalSuccess] = useState('');
  const [savingIp, setSavingIp] = useState(false);

  // New License modal state
  const [newLicModalOpen, setNewLicModalOpen] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState('business');
  const [newServerName, setNewServerName] = useState('');
  const [newServerIp, setNewServerIp] = useState('');
  const [creatingLic, setCreatingLic] = useState(false);

  // Invoices & Billing state
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [payingInvoiceId, setPayingInvoiceId] = useState<number | null>(null);
  const [newInvoiceModalOpen, setNewInvoiceModalOpen] = useState(false);
  const [invoicePlan, setInvoicePlan] = useState('business');
  const [invoicePeriod, setInvoicePeriod] = useState<'monthly' | 'halfYear' | 'year'>('monthly');
  const [invoicePaymentMethod, setInvoicePaymentMethod] = useState<'card' | 'sbp' | 'crypto'>('card');
  const [creatingInvoice, setCreatingInvoice] = useState(false);
  const [billingToast, setBillingToast] = useState<string | null>(null);

  // Telemetry state
  const [telemetryPoints, setTelemetryPoints] = useState<TelemetryPoint[]>([]);
  const [loadingTelemetry, setLoadingTelemetry] = useState(false);
  const [sendingHeartbeat, setSendingHeartbeat] = useState(false);

  // Launcher Builder state
  const [builderProjectName, setBuilderProjectName] = useState('Держава Онлайн');
  const [builderPrimaryColor, setBuilderPrimaryColor] = useState('#ff3d8a');
  const [builderServerIp, setBuilderServerIp] = useState('188.127.229.224');
  const [builderServerPort, setBuilderServerPort] = useState('7788');
  const [buildingLauncher, setBuildingLauncher] = useState(false);
  const [buildResult, setBuildResult] = useState<LauncherBuildResult | null>(null);

  useEffect(() => {
    loadDashboardData();
  }, []);

  useEffect(() => {
    if (activeTab === 'billing') {
      loadInvoices();
    } else if (activeTab === 'telemetry') {
      loadTelemetry();
    }
  }, [activeTab, licenses]);

  const loadDashboardData = async () => {
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
        const lics = licData.licenses || [];
        setLicenses(lics);
        if (lics.length > 0 && lics[0].bound_ip && lics[0].bound_ip !== '0.0.0.0') {
          setBuilderServerIp(lics[0].bound_ip);
        }
      }
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadInvoices = async () => {
    setLoadingInvoices(true);
    try {
      const res = await fetch('/api/v1/billing/invoices');
      if (res.ok) {
        const data = await res.json();
        setInvoices(data.invoices || []);
      }
    } catch (err) {
      console.error('Failed to load invoices:', err);
    } finally {
      setLoadingInvoices(false);
    }
  };

  const loadTelemetry = async () => {
    if (licenses.length === 0) return;
    const lic = licenses[0];
    setLoadingTelemetry(true);
    try {
      const res = await fetch(`/api/v1/telemetry/stats?key=${encodeURIComponent(lic.license_key)}`);
      if (res.ok) {
        const data = await res.json();
        setTelemetryPoints(data.stats || []);
      }
    } catch (err) {
      console.error('Failed to load telemetry:', err);
    } finally {
      setLoadingTelemetry(false);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(text);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const copyPromoCode = (code: string) => {
    navigator.clipboard.writeText(code);
    setCopiedPromo(true);
    setTimeout(() => setCopiedPromo(false), 2000);
  };

  const openIpModal = (lic: License) => {
    setEditingLicense(lic);
    setInputIp(lic.bound_ip === '0.0.0.0' ? '' : lic.bound_ip);
    setInputServerName(lic.server_name);
    setIpModalError('');
    setIpModalSuccess('');
  };

  const handleSaveIp = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingLicense) return;
    setIpModalError('');
    setIpModalSuccess('');
    setSavingIp(true);

    try {
      const res = await fetch('/api/v1/license/bind-ip', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: editingLicense.id,
          serverIp: inputIp.trim() || '0.0.0.0',
          serverName: inputServerName.trim() || editingLicense.server_name,
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Ошибка привязки IP');
      }

      setIpModalSuccess('IP-адрес успешно сохранён!');
      setTimeout(() => {
        setEditingLicense(null);
        loadDashboardData();
      }, 1000);
    } catch (err: any) {
      setIpModalError(err.message);
    } finally {
      setSavingIp(false);
    }
  };

  const handleCreateLicense = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingLic(true);

    try {
      const res = await fetch('/api/v1/license/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          plan: selectedPlan,
          serverName: newServerName.trim() || `${user?.username} RP Server`,
          boundIp: newServerIp.trim() || '0.0.0.0',
        }),
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.error || 'Ошибка создания лицензии');
      }

      setNewLicModalOpen(false);
      setNewServerName('');
      setNewServerIp('');
      loadDashboardData();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setCreatingLic(false);
    }
  };

  const handlePayInvoice = async (invoiceId: number) => {
    setPayingInvoiceId(invoiceId);
    try {
      const res = await fetch('/api/v1/billing/pay', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ invoiceId }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Ошибка оплаты');
      }

      setBillingToast(data.message || 'Оплата успешно завершена!');
      setTimeout(() => setBillingToast(null), 4000);
      loadInvoices();
      loadDashboardData();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setPayingInvoiceId(null);
    }
  };

  const handleCreateInvoiceSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingInvoice(true);
    try {
      const primaryLic = licenses[0];
      const res = await fetch('/api/v1/billing/create-invoice', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: primaryLic?.id,
          plan: invoicePlan,
          period: invoicePeriod,
          paymentMethod: invoicePaymentMethod,
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Ошибка выставления счёта');
      }

      setNewInvoiceModalOpen(false);
      setBillingToast(`Счёт №${data.invoiceId} на сумму ${data.amount.toLocaleString()} ₽ выставлен!`);
      setTimeout(() => setBillingToast(null), 4000);
      loadInvoices();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setCreatingInvoice(false);
    }
  };

  const handleSendHeartbeat = async () => {
    if (licenses.length === 0) return;
    setSendingHeartbeat(true);
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
        loadTelemetry();
      }
    } catch (err) {
      console.error('Failed to send heartbeat:', err);
    } finally {
      setSendingHeartbeat(false);
    }
  };

  const handleBuildLauncher = async (e: React.FormEvent) => {
    e.preventDefault();
    if (licenses.length === 0) {
      alert('Для сборки лаунчера требуется активная лицензия');
      return;
    }

    setBuildingLauncher(true);
    setBuildResult(null);

    try {
      const lic = licenses[0];
      const res = await fetch('/api/v1/launcher/build', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseId: lic.id,
          projectName: builderProjectName,
          primaryColor: builderPrimaryColor,
          serverIp: builderServerIp,
          serverPort: Number(builderServerPort),
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Ошибка сборки лаунчера');
      }

      setBuildResult(data);
    } catch (err: any) {
      alert(err.message);
    } finally {
      setBuildingLauncher(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[70vh] flex flex-col items-center justify-center gap-3">
        <RefreshCw className="w-8 h-8 text-brand animate-spin" />
        <p className="text-gray-400 text-sm">Загрузка панели управления FloV:MP...</p>
      </div>
    );
  }

  const primaryLic = licenses[0];
  const isIpBound = primaryLic && primaryLic.bound_ip !== '0.0.0.0';
  const promoCode = `FLOV-${user?.username.toUpperCase() || 'REF20'}`;
  const latestTelemetry = telemetryPoints.length > 0 ? telemetryPoints[telemetryPoints.length - 1] : null;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Toast alert */}
      {billingToast && (
        <div className="fixed top-20 right-6 z-50 p-4 rounded-2xl bg-surface-200 border border-emerald-500/40 text-emerald-400 text-sm font-semibold shadow-2xl flex items-center gap-3 animate-fade-in">
          <CheckCircle2 className="w-5 h-5 shrink-0" />
          <span>{billingToast}</span>
        </div>
      )}

      {/* Top Banner & Profile Overview (icsnotify style) */}
      <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 mb-8 flex flex-col md:flex-row items-start md:items-center justify-between gap-6 shadow-glass">
        <div className="flex items-center gap-4">
          <div className="relative w-14 h-14 rounded-2xl overflow-hidden border border-brand/50 shadow-neon-pink bg-surface-300">
            <img
              src="/branding/logo-codex.png"
              alt="Profile avatar"
              className="w-full h-full object-cover"
            />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-black text-white">Здравствуйте, {user?.username}</h1>
              <span className="px-2.5 py-0.5 rounded-full bg-brand/20 border border-brand/40 text-brand text-xs font-mono font-semibold uppercase">
                {user?.role === 'admin' ? 'Администратор' : 'Клиент SaaS'}
              </span>
            </div>
            <p className="text-sm text-gray-400 mt-1">
              Управление лицензиями, FastDL VDS, биллингом и брендированным лаунчером FloV:MP
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => setNewLicModalOpen(true)}
            className="px-5 py-3 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white text-sm font-bold shadow-neon-pink flex items-center gap-2 transition-all"
          >
            <Plus className="w-4 h-4" />
            <span>Новая лицензия</span>
          </button>
        </div>
      </div>

      {/* Tabs Navigation */}
      <div className="flex items-center gap-2 overflow-x-auto pb-2 mb-8 border-b border-white/10">
        <button
          onClick={() => setActiveTab('overview')}
          className={`px-4 py-2.5 rounded-xl text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-2 transition-all shrink-0 ${
            activeTab === 'overview'
              ? 'bg-brand text-white shadow-neon-pink'
              : 'bg-surface-300 text-gray-400 hover:text-white hover:bg-surface-200'
          }`}
        >
          <Key className="w-4 h-4" />
          <span>Ключи и статус</span>
        </button>

        <button
          onClick={() => setActiveTab('telemetry')}
          className={`px-4 py-2.5 rounded-xl text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-2 transition-all shrink-0 ${
            activeTab === 'telemetry'
              ? 'bg-brand text-white shadow-neon-pink'
              : 'bg-surface-300 text-gray-400 hover:text-white hover:bg-surface-200'
          }`}
        >
          <Activity className="w-4 h-4" />
          <span>Телеметрия VDS</span>
        </button>

        <button
          onClick={() => setActiveTab('builder')}
          className={`px-4 py-2.5 rounded-xl text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-2 transition-all shrink-0 ${
            activeTab === 'builder'
              ? 'bg-brand text-white shadow-neon-pink'
              : 'bg-surface-300 text-gray-400 hover:text-white hover:bg-surface-200'
          }`}
        >
          <Layers className="w-4 h-4" />
          <span>Сборщик лаунчера</span>
        </button>

        <button
          onClick={() => setActiveTab('billing')}
          className={`px-4 py-2.5 rounded-xl text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-2 transition-all shrink-0 ${
            activeTab === 'billing'
              ? 'bg-brand text-white shadow-neon-pink'
              : 'bg-surface-300 text-gray-400 hover:text-white hover:bg-surface-200'
          }`}
        >
          <CreditCard className="w-4 h-4" />
          <span>Счета и оплата</span>
        </button>

        <button
          onClick={() => setActiveTab('affiliate')}
          className={`px-4 py-2.5 rounded-xl text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-2 transition-all shrink-0 ${
            activeTab === 'affiliate'
              ? 'bg-brand text-white shadow-neon-pink'
              : 'bg-surface-300 text-gray-400 hover:text-white hover:bg-surface-200'
          }`}
        >
          <Percent className="w-4 h-4" />
          <span>Партнёрка (20%)</span>
        </button>
      </div>

      {/* TAB 1: OVERVIEW & LICENSES */}
      {activeTab === 'overview' && (
        <div className="space-y-8 animate-fade-in">
          {/* Quick Start Onboarding Tracker */}
          <div className="glass-panel p-6 rounded-3xl border border-white/10 shadow-glass">
            <h3 className="text-xs font-mono uppercase tracking-widest text-brand font-bold mb-4">
              Быстрый старт проекта
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {/* Step 1 */}
              <div className="p-4 rounded-2xl bg-surface-300 border border-white/5 flex items-center gap-3">
                <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0" />
                <div>
                  <div className="text-xs font-bold text-white">Аккаунт создан</div>
                  <div className="text-[11px] text-gray-400">{user?.email}</div>
                </div>
              </div>

              {/* Step 2 */}
              <div className="p-4 rounded-2xl bg-surface-300 border border-white/5 flex items-center gap-3">
                {primaryLic ? (
                  <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0" />
                ) : (
                  <Circle className="w-5 h-5 text-gray-500 shrink-0" />
                )}
                <div>
                  <div className="text-xs font-bold text-white">Лицензия активна</div>
                  <div className="text-[11px] text-gray-400">{primaryLic ? `Тариф: ${primaryLic.plan}` : 'Не создана'}</div>
                </div>
              </div>

              {/* Step 3 */}
              <div className="p-4 rounded-2xl bg-surface-300 border border-white/5 flex items-center gap-3">
                {isIpBound ? (
                  <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0" />
                ) : (
                  <Circle className="w-5 h-5 text-amber-400 shrink-0" />
                )}
                <div>
                  <div className="text-xs font-bold text-white">IP сервера привязан</div>
                  <div className="text-[11px] text-gray-400">{isIpBound ? primaryLic.bound_ip : 'Требуется привязать'}</div>
                </div>
              </div>

              {/* Step 4 */}
              <div className="p-4 rounded-2xl bg-surface-300 border border-white/5 flex items-center gap-3">
                <CheckCircle2 className="w-5 h-5 text-cyan-neon shrink-0" />
                <div>
                  <div className="text-xs font-bold text-white">Сетевой узел онлайн</div>
                  <div className="text-[11px] text-gray-400">Порт UDP 7788 FastDL</div>
                </div>
              </div>
            </div>
          </div>

          {/* Licenses List Section */}
          <div>
            <div className="flex items-center justify-between mb-6">
              <div>
                <h2 className="text-xl font-black text-white flex items-center gap-2">
                  <Key className="w-5 h-5 text-brand" />
                  <span>Ваши активные лицензии</span>
                </h2>
                <p className="text-xs text-gray-400 mt-1">
                  Каждый ключ привязывается к VDS/Dedicated IP игрового сервера
                </p>
              </div>
              <span className="text-xs font-mono text-gray-400">
                Всего лицензий: {licenses.length}
              </span>
            </div>

            {licenses.length === 0 ? (
              <div className="glass-panel p-10 rounded-2xl border border-white/10 text-center">
                <Key className="w-10 h-10 text-gray-500 mx-auto mb-3" />
                <p className="text-gray-300 font-medium">У вас пока нет активных лицензий</p>
                <button
                  onClick={() => setNewLicModalOpen(true)}
                  className="mt-4 px-4 py-2 rounded-xl bg-brand text-white text-xs font-bold"
                >
                  Создать первую лицензию
                </button>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-6">
                {licenses.map((lic) => {
                  const isExpired = new Date(lic.expires_at).getTime() < Date.now();
                  const isKeyRevealed = showKeyId === lic.id;

                  return (
                    <div
                      key={lic.id}
                      className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 hover:border-brand/30 transition-all shadow-glass"
                    >
                      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
                        {/* Left: Info */}
                        <div className="space-y-3 flex-1">
                          <div className="flex items-center gap-3">
                            <h3 className="text-lg font-bold text-white">{lic.server_name}</h3>
                            <span
                              className={`text-xs px-2.5 py-0.5 rounded-full font-mono font-bold uppercase ${
                                lic.plan === 'enterprise'
                                  ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30'
                                  : lic.plan === 'business'
                                  ? 'bg-brand/20 text-brand border border-brand/30'
                                  : 'bg-gray-700/50 text-gray-300 border border-gray-600'
                              }`}
                            >
                              Тариф: {lic.plan}
                            </span>

                            <span
                              className={`text-xs px-2.5 py-0.5 rounded-full font-mono flex items-center gap-1.5 ${
                                lic.is_active && !isExpired
                                  ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                  : 'bg-red-500/20 text-red-400 border border-red-500/30'
                              }`}
                            >
                              <span
                                className={`w-1.5 h-1.5 rounded-full ${
                                  lic.is_active && !isExpired ? 'bg-emerald-400 animate-pulse' : 'bg-red-400'
                                }`}
                              ></span>
                              {lic.is_active && !isExpired ? 'АКТИВНА' : 'ПРИОСТАНОВЛЕНА'}
                            </span>
                          </div>

                          {/* License Key Box */}
                          <div className="flex items-center gap-2 max-w-xl">
                            <div className="flex-1 px-4 py-2.5 bg-surface-300 rounded-xl border border-white/10 font-mono text-sm text-gray-200 flex items-center justify-between">
                              <span className="tracking-wider">
                                {isKeyRevealed ? lic.license_key : `${lic.license_key.slice(0, 4)}-••••-••••-••••`}
                              </span>
                              <button
                                onClick={() => setShowKeyId(isKeyRevealed ? null : lic.id)}
                                className="text-gray-400 hover:text-white transition-colors"
                                title={isKeyRevealed ? 'Скрыть ключ' : 'Показать ключ'}
                              >
                                {isKeyRevealed ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                              </button>
                            </div>

                            <button
                              onClick={() => copyToClipboard(lic.license_key)}
                              className="p-2.5 rounded-xl bg-surface-300 border border-white/10 hover:border-brand/40 text-gray-300 hover:text-white transition-colors"
                              title="Скопировать ключ"
                            >
                              {copiedKey === lic.license_key ? (
                                <Check className="w-4 h-4 text-emerald-400" />
                              ) : (
                                <Copy className="w-4 h-4" />
                              )}
                            </button>
                          </div>

                          {/* Meta Pills */}
                          <div className="flex flex-wrap items-center gap-4 text-xs text-gray-400 pt-1 font-mono">
                            <div className="flex items-center gap-1.5">
                              <Globe className="w-3.5 h-3.5 text-brand" />
                              <span>
                                Привязанный IP: <strong className="text-white">{lic.bound_ip === '0.0.0.0' ? 'Любой IP (0.0.0.0)' : lic.bound_ip}</strong>
                              </span>
                            </div>

                            <div className="flex items-center gap-1.5">
                              <Server className="w-3.5 h-3.5 text-blue-400" />
                              <span>Слоты: <strong className="text-white">{lic.max_players} игроков</strong></span>
                            </div>

                            <div className="flex items-center gap-1.5">
                              <Clock className="w-3.5 h-3.5 text-purple-400" />
                              <span>
                                Истекает: <strong className="text-white">{new Date(lic.expires_at).toLocaleDateString('ru-RU')}</strong>
                              </span>
                            </div>
                          </div>
                        </div>

                        {/* Right: Actions */}
                        <div className="flex items-center gap-3 shrink-0">
                          <button
                            onClick={() => openIpModal(lic)}
                            className="px-4 py-2.5 rounded-xl bg-surface-300 border border-white/15 hover:border-brand/40 text-white text-xs font-semibold flex items-center gap-2 transition-colors"
                          >
                            <Settings className="w-4 h-4 text-brand" />
                            <span>Настроить IP</span>
                          </button>

                          <a
                            href="/cdn/FloVMP-Server-x64-Linux.tar.gz"
                            download
                            className="px-4 py-2.5 rounded-xl bg-white/5 border border-white/10 hover:bg-white/10 text-gray-300 hover:text-white text-xs font-semibold flex items-center gap-2 transition-colors"
                          >
                            <Download className="w-4 h-4 text-emerald-400" />
                            <span>Скачать сервер</span>
                          </a>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Guide & Quick Setup */}
          <div className="glass-panel p-8 rounded-3xl border border-white/10">
            <h3 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
              <Terminal className="w-5 h-5 text-brand" />
              <span>Инструкция по установке на VDS/Dedicated</span>
            </h3>
            <ol className="space-y-4 text-sm text-gray-300 list-decimal list-inside">
              <li className="leading-relaxed">
                <strong className="text-white">Скачайте дистрибутив:</strong> Нажмите «Скачать сервер» выше для загрузки чистого пакета движка под Linux (Ubuntu/Debian) или Windows.
              </li>
              <li className="leading-relaxed">
                <strong className="text-white">Пропишите лицензионный ключ:</strong> Откройте конфигурационный файл <code className="text-brand font-mono text-xs bg-surface-300 px-1.5 py-0.5 rounded">server.toml</code> и укажите:
                <pre className="mt-2 p-3 bg-surface-300 rounded-xl font-mono text-xs text-gray-200 overflow-x-auto">
{`[licensing]
key = "${licenses[0]?.license_key || 'FLV-XXXX-XXXX-XXXX'}"
bound_ip = "${licenses[0]?.bound_ip === '0.0.0.0' ? '188.127.229.224' : licenses[0]?.bound_ip || '188.127.229.224'}"`}
                </pre>
              </li>
              <li className="leading-relaxed">
                <strong className="text-white">Запустите рантайм:</strong> Выполните команду <code className="text-emerald-400 font-mono text-xs bg-surface-300 px-1.5 py-0.5 rounded">./start.sh</code>. Сервер мгновенно пройдет проверку в API FloV:MP и начнет прием игроков.
              </li>
            </ol>
          </div>
        </div>
      )}

      {/* TAB 2: TELEMETRY & VDS MONITORING */}
      {activeTab === 'telemetry' && (
        <div className="space-y-8 animate-fade-in">
          {/* Header & Simulated Heartbeat Trigger */}
          <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 flex flex-col md:flex-row items-start md:items-center justify-between gap-6 shadow-glass">
            <div>
              <h2 className="text-xl font-black text-white flex items-center gap-2">
                <Activity className="w-5 h-5 text-brand" />
                <span>Мониторинг игрового узла и телеметрия</span>
              </h2>
              <p className="text-xs text-gray-400 mt-1">
                Ключ: <code className="text-brand font-mono">{primaryLic?.license_key || 'Нет ключа'}</code> • VDS: <strong className="text-white">{primaryLic?.bound_ip || '188.127.229.224'}</strong>
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                onClick={loadTelemetry}
                disabled={loadingTelemetry}
                className="px-4 py-2.5 rounded-xl bg-surface-300 border border-white/10 hover:border-brand/40 text-gray-300 hover:text-white text-xs font-semibold flex items-center gap-2 transition-all"
              >
                <RefreshCw className={`w-4 h-4 ${loadingTelemetry ? 'animate-spin text-brand' : ''}`} />
                <span>Обновить</span>
              </button>

              <button
                onClick={handleSendHeartbeat}
                disabled={sendingHeartbeat || !primaryLic}
                className="px-4 py-2.5 rounded-xl bg-brand/20 border border-brand/40 hover:bg-brand/30 text-brand text-xs font-bold flex items-center gap-2 transition-all shadow-neon-pink"
              >
                <Zap className="w-4 h-4" />
                <span>{sendingHeartbeat ? 'Отправка тика...' : 'Отправить тестовый heartbeat'}</span>
              </button>
            </div>
          </div>

          {/* Metric Cards Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="flex items-center justify-between text-gray-400 mb-2">
                <span className="text-xs font-mono uppercase">Tick Rate</span>
                <Cpu className="w-4 h-4 text-emerald-400" />
              </div>
              <div className="text-3xl font-black text-white">
                {latestTelemetry ? `${latestTelemetry.tick_rate}.0` : '60.0'} <span className="text-xs font-normal text-gray-400 font-mono">Hz</span>
              </div>
              <div className="text-[11px] text-emerald-400 mt-2 font-mono flex items-center gap-1">
                <CheckCircle2 className="w-3.5 h-3.5" />
                <span>Синхронизация 16.6 ms</span>
              </div>
            </div>

            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="flex items-center justify-between text-gray-400 mb-2">
                <span className="text-xs font-mono uppercase">Server Engine FPS</span>
                <Activity className="w-4 h-4 text-brand" />
              </div>
              <div className="text-3xl font-black text-white">
                {latestTelemetry ? `${latestTelemetry.fps}.0` : '60.0'} <span className="text-xs font-normal text-gray-400 font-mono">FPS</span>
              </div>
              <div className="text-[11px] text-gray-400 mt-2 font-mono">
                Рендеринг физики без просадок
              </div>
            </div>

            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="flex items-center justify-between text-gray-400 mb-2">
                <span className="text-xs font-mono uppercase">RAM CoreCLR</span>
                <HardDrive className="w-4 h-4 text-cyan-300" />
              </div>
              <div className="text-3xl font-black text-white">
                {latestTelemetry ? `${latestTelemetry.memory_mb}` : '248'} <span className="text-xs font-normal text-gray-400 font-mono">MB</span>
              </div>
              <div className="text-[11px] text-cyan-300 mt-2 font-mono">
                ОЗУ оптимизировано
              </div>
            </div>

            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="flex items-center justify-between text-gray-400 mb-2">
                <span className="text-xs font-mono uppercase">Игроки онлайн</span>
                <Users className="w-4 h-4 text-purple-400" />
              </div>
              <div className="text-3xl font-black text-white">
                {latestTelemetry ? latestTelemetry.players : 1} <span className="text-xs font-normal text-gray-400 font-mono">/ {primaryLic?.max_players || 1500}</span>
              </div>
              <div className="text-[11px] text-purple-300 mt-2 font-mono">
                Слоты активны
              </div>
            </div>
          </div>

          {/* Telemetry Packets Log */}
          <div className="glass-panel p-6 rounded-3xl border border-white/10 shadow-glass">
            <h3 className="text-base font-bold text-white mb-4 flex items-center gap-2">
              <Wifi className="w-4 h-4 text-cyan-300" />
              <span>История пакетов телеметрии (последние 30 тиков)</span>
            </h3>

            {telemetryPoints.length === 0 ? (
              <div className="p-8 text-center text-gray-400 text-xs">
                <p>Нет записанных пакетов телеметрии для этой лицензии.</p>
                <p className="mt-1 text-gray-500">
                  Нажмите кнопку «Отправить тестовый heartbeat» выше для отправки тестового пакета со стенда.
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs font-mono">
                  <thead>
                    <tr className="text-gray-400 border-b border-white/10">
                      <th className="pb-3 px-3">Время</th>
                      <th className="pb-3 px-3">IP Узла</th>
                      <th className="pb-3 px-3">Онлайн</th>
                      <th className="pb-3 px-3">Tick Rate</th>
                      <th className="pb-3 px-3">FPS</th>
                      <th className="pb-3 px-3">ОЗУ</th>
                      <th className="pb-3 px-3">Статус</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5 text-gray-300">
                    {telemetryPoints.map((pt) => (
                      <tr key={pt.id} className="hover:bg-white/5">
                        <td className="py-2.5 px-3 text-white">
                          {new Date(pt.recorded_at).toLocaleTimeString('ru-RU')}
                        </td>
                        <td className="py-2.5 px-3 text-gray-400">{pt.server_ip}</td>
                        <td className="py-2.5 px-3 text-brand font-bold">
                          {pt.players} / {pt.max_players}
                        </td>
                        <td className="py-2.5 px-3 text-emerald-400">{pt.tick_rate} Hz</td>
                        <td className="py-2.5 px-3">{pt.fps} FPS</td>
                        <td className="py-2.5 px-3 text-cyan-300">{pt.memory_mb} MB</td>
                        <td className="py-2.5 px-3">
                          <span className="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 text-[10px]">
                            OK
                          </span>
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

      {/* TAB 3: CUSTOM LAUNCHER BUILDER */}
      {activeTab === 'builder' && (
        <div className="space-y-8 animate-fade-in">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
            {/* Builder Configuration Form */}
            <div className="glass-panel p-8 rounded-3xl border border-white/10 shadow-glass">
              <div className="flex items-center gap-3 mb-2">
                <div className="p-2.5 rounded-xl bg-cyan-500/10 border border-cyan-500/30 text-cyan-300">
                  <Layers className="w-5 h-5" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-white">Сборщик кастомного лаунчера</h2>
                  <p className="text-xs text-gray-400">
                    Автоматическая компиляция установщика под бренд и IP вашего проекта
                  </p>
                </div>
              </div>

              <form onSubmit={handleBuildLauncher} className="space-y-5 mt-6">
                <div>
                  <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                    Название проекта в лаунчере
                  </label>
                  <input
                    type="text"
                    required
                    value={builderProjectName}
                    onChange={(e) => setBuilderProjectName(e.target.value)}
                    placeholder="Например: Держава Онлайн"
                    className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm focus:border-brand transition-colors"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                    Фирменный акцентный цвет
                  </label>
                  <div className="flex items-center gap-3">
                    <input
                      type="color"
                      value={builderPrimaryColor}
                      onChange={(e) => setBuilderPrimaryColor(e.target.value)}
                      className="w-10 h-10 rounded-xl bg-surface-300 border border-white/10 cursor-pointer"
                    />
                    <input
                      type="text"
                      value={builderPrimaryColor}
                      onChange={(e) => setBuilderPrimaryColor(e.target.value)}
                      className="flex-1 px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                    />
                  </div>

                  {/* Preset Colors */}
                  <div className="flex items-center gap-2 mt-2">
                    <span className="text-[11px] text-gray-400">Пресеты:</span>
                    {[
                      { name: 'FloV Pink', hex: '#ff3d8a' },
                      { name: 'Majestic Cyan', hex: '#00f0ff' },
                      { name: 'Amber Gold', hex: '#f59e0b' },
                      { name: 'Emerald', hex: '#10b981' },
                      { name: 'Royal Purple', hex: '#8b5cf6' },
                    ].map((p) => (
                      <button
                        key={p.hex}
                        type="button"
                        onClick={() => setBuilderPrimaryColor(p.hex)}
                        className="px-2 py-0.5 rounded-lg border border-white/10 text-[10px] font-mono hover:border-white/30"
                        style={{ color: p.hex }}
                      >
                        {p.name}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                      IP Сервера
                    </label>
                    <input
                      type="text"
                      required
                      value={builderServerIp}
                      onChange={(e) => setBuilderServerIp(e.target.value)}
                      placeholder="188.127.229.224"
                      className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                      UDP Порт
                    </label>
                    <input
                      type="text"
                      required
                      value={builderServerPort}
                      onChange={(e) => setBuilderServerPort(e.target.value)}
                      placeholder="7788"
                      className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                    />
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={buildingLauncher || licenses.length === 0}
                  className="w-full py-3.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white font-bold text-sm shadow-neon-pink flex items-center justify-center gap-2 transition-all disabled:opacity-50"
                >
                  {buildingLauncher ? (
                    <>
                      <RefreshCw className="w-4 h-4 animate-spin" />
                      <span>Компиляция установщика лаунчера...</span>
                    </>
                  ) : (
                    <>
                      <ShieldCheck className="w-4 h-4" />
                      <span>Скомпилировать лаунчер проекта</span>
                    </>
                  )}
                </button>
              </form>
            </div>

            {/* Build Status & Ready Artifacts */}
            <div className="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between shadow-glass">
              <div>
                <h3 className="text-base font-bold text-white mb-2 flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-brand" />
                  <span>Результат компиляции</span>
                </h3>
                <p className="text-xs text-gray-400 mb-6">
                  После сборки вы получаете прямой линк для загрузки установщика вашими игроками
                </p>

                {buildResult ? (
                  <div className="p-5 rounded-2xl bg-emerald-500/10 border border-emerald-500/30 text-xs space-y-4 animate-fade-in">
                    <div className="flex items-center gap-2 text-emerald-400 font-bold">
                      <CheckCircle2 className="w-5 h-5" />
                      <span>Лаунчер успешно собран (Build #{buildResult.buildId})</span>
                    </div>

                    <div className="space-y-1.5 font-mono text-gray-300">
                      <div>Проект: <strong className="text-white">{buildResult.projectName}</strong></div>
                      <div>Акцентный цвет: <span className="text-white" style={{ color: buildResult.primaryColor }}>{buildResult.primaryColor}</span></div>
                      <div>Эндпоинт: <strong className="text-white">{buildResult.config.serverIp}:{buildResult.config.serverPort}</strong></div>
                      <div>Лицензия: <strong className="text-white">{buildResult.config.licenseKey}</strong></div>
                    </div>

                    <a
                      href={buildResult.downloadUrl}
                      download
                      className="block w-full py-3 rounded-xl bg-emerald-500 hover:bg-emerald-600 text-black font-bold text-center text-xs shadow-lg transition-all"
                    >
                      Скачать {buildResult.projectName}-Setup.exe
                    </a>
                  </div>
                ) : (
                  <div className="p-8 rounded-2xl bg-surface-300 border border-white/5 text-center text-gray-400 text-xs">
                    <Layers className="w-10 h-10 text-gray-500 mx-auto mb-3" />
                    <p className="font-semibold text-gray-300">Ожидание запуска сборки</p>
                    <p className="mt-1 text-gray-500">
                      Укажите параметры слева и нажмите «Скомпилировать лаунчер проекта». Билдер упакует Electron + C# Native Bridge и FastDL манифест.
                    </p>
                  </div>
                )}
              </div>

              {/* Architecture Feature List */}
              <div className="pt-6 border-t border-white/10 space-y-2 text-xs text-gray-400 font-mono">
                <div className="flex items-center gap-2">
                  <Check className="w-3.5 h-3.5 text-brand" />
                  <span>Аппаратное ускорение Chromium UI (Majestic Blur / Shadows)</span>
                </div>
                <div className="flex items-center gap-2">
                  <Check className="w-3.5 h-3.5 text-brand" />
                  <span>Прямой коннектор C# .NET 8 (FloVMP.Connect)</span>
                </div>
                <div className="flex items-center gap-2">
                  <Check className="w-3.5 h-3.5 text-brand" />
                  <span>Вшитый FastDL кэш с CDN VDS</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 4: BILLING & INVOICES */}
      {activeTab === 'billing' && (
        <div className="space-y-8 animate-fade-in">
          {/* Billing Overview Header */}
          <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 flex flex-col md:flex-row items-start md:items-center justify-between gap-6 shadow-glass">
            <div>
              <h2 className="text-xl font-black text-white flex items-center gap-2">
                <CreditCard className="w-5 h-5 text-brand" />
                <span>Счета и управление подпиской</span>
              </h2>
              <p className="text-xs text-gray-400 mt-1">
                Все счета формируются автоматически, продление лицензий происходит моментально
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                onClick={loadInvoices}
                disabled={loadingInvoices}
                className="px-4 py-2.5 rounded-xl bg-surface-300 border border-white/10 hover:border-brand/40 text-gray-300 hover:text-white text-xs font-semibold flex items-center gap-2 transition-all"
              >
                <RefreshCw className={`w-4 h-4 ${loadingInvoices ? 'animate-spin text-brand' : ''}`} />
                <span>Обновить</span>
              </button>

              <button
                onClick={() => setNewInvoiceModalOpen(true)}
                className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white text-xs font-bold shadow-neon-pink flex items-center gap-2 transition-all"
              >
                <Plus className="w-4 h-4" />
                <span>Выставить счёт на продление</span>
              </button>
            </div>
          </div>

          {/* Invoices Table */}
          <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 shadow-glass">
            <h3 className="text-base font-bold text-white mb-4 flex items-center gap-2">
              <History className="w-4 h-4 text-purple-400" />
              <span>История выставленных счетов</span>
            </h3>

            {invoices.length === 0 ? (
              <div className="p-8 text-center text-gray-400 text-xs">
                <p>У вас пока нет счетов.</p>
                <button
                  onClick={() => setNewInvoiceModalOpen(true)}
                  className="mt-3 px-4 py-2 rounded-xl bg-brand text-white text-xs font-bold"
                >
                  Выставить счёт
                </button>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs font-mono">
                  <thead>
                    <tr className="text-gray-400 border-b border-white/10">
                      <th className="pb-3 px-3">№ Счёта</th>
                      <th className="pb-3 px-3">Тариф</th>
                      <th className="pb-3 px-3">Сумма</th>
                      <th className="pb-3 px-3">Метод</th>
                      <th className="pb-3 px-3">Дата создания</th>
                      <th className="pb-3 px-3">Статус</th>
                      <th className="pb-3 px-3 text-right">Действие</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5 text-gray-300">
                    {invoices.map((inv) => {
                      const isPaid = inv.status === 'paid';

                      return (
                        <tr key={inv.id} className="hover:bg-white/5">
                          <td className="py-3 px-3 text-white font-bold">#{inv.id}</td>
                          <td className="py-3 px-3 uppercase text-brand">{inv.plan}</td>
                          <td className="py-3 px-3 text-white font-bold text-sm">
                            {inv.amount_rub.toLocaleString()} ₽
                          </td>
                          <td className="py-3 px-3 text-gray-400 capitalize">{inv.payment_method}</td>
                          <td className="py-3 px-3 text-gray-400">
                            {new Date(inv.created_at).toLocaleDateString('ru-RU')}
                          </td>
                          <td className="py-3 px-3">
                            <span
                              className={`px-2.5 py-0.5 rounded-full text-[11px] font-bold ${
                                isPaid
                                  ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                  : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                              }`}
                            >
                              {isPaid ? 'ОПЛАЧЕН' : 'ОЖИДАЕТ ОПЛАТЫ'}
                            </span>
                          </td>
                          <td className="py-3 px-3 text-right">
                            {isPaid ? (
                              <span className="text-gray-500 text-[11px]">Закрыт</span>
                            ) : (
                              <button
                                onClick={() => handlePayInvoice(inv.id)}
                                disabled={payingInvoiceId === inv.id}
                                className="px-3 py-1.5 rounded-lg bg-emerald-500 hover:bg-emerald-600 text-black font-bold text-[11px] transition-colors"
                              >
                                {payingInvoiceId === inv.id ? 'Оплата...' : 'Оплатить'}
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

      {/* TAB 5: AFFILIATE PROGRAM */}
      {activeTab === 'affiliate' && (
        <div className="space-y-8 animate-fade-in">
          {/* Referral Banner (icsnotify.ru style) */}
          <div className="glass-panel p-8 rounded-3xl border border-white/10 shadow-glass">
            <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
              <div className="space-y-2 max-w-2xl">
                <div className="flex items-center gap-2 text-brand font-mono text-xs font-bold uppercase tracking-wider">
                  <Percent className="w-4 h-4" />
                  <span>Партнёрская программа SaaS</span>
                </div>
                <h3 className="text-2xl font-black text-white">Зарабатывайте 20% от оплат серверов</h3>
                <p className="text-xs text-gray-400 leading-relaxed">
                  Рекомендуйте автономный мультиплеер FloV:MP владельцам GTA V RP серверов. Вы получаете <strong className="text-white">20% комиссионных</strong> с каждого продления лицензии приглашённого проекта пожизненно. Приглашённый проект получает скидку <strong className="text-emerald-400">10%</strong>.
                </p>
              </div>

              <div className="flex items-center gap-2 shrink-0">
                <div className="px-4 py-2.5 rounded-xl bg-surface-300 border border-white/10 font-mono text-sm font-bold text-brand">
                  {promoCode}
                </div>
                <button
                  onClick={() => copyPromoCode(promoCode)}
                  className="px-4 py-2.5 rounded-xl bg-brand/20 border border-brand/40 text-brand hover:bg-brand/30 text-xs font-bold flex items-center gap-1.5 transition-colors"
                >
                  {copiedPromo ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
                  <span>{copiedPromo ? 'Скопировано' : 'Копировать промокод'}</span>
                </button>
              </div>
            </div>
          </div>

          {/* Referral Stats Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="text-xs font-mono uppercase text-gray-400 mb-1">Приглашено серверов</div>
              <div className="text-3xl font-black text-white">0</div>
              <div className="text-[11px] text-gray-500 mt-2 font-mono">Активные рефералы</div>
            </div>

            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="text-xs font-mono uppercase text-gray-400 mb-1">Начислено вознаграждений</div>
              <div className="text-3xl font-black text-emerald-400">0 ₽</div>
              <div className="text-[11px] text-emerald-500/80 mt-2 font-mono">Доступно к выводу</div>
            </div>

            <div className="glass-panel p-6 rounded-2xl border border-white/10">
              <div className="text-xs font-mono uppercase text-gray-400 mb-1">Процент отчислений</div>
              <div className="text-3xl font-black text-brand">20%</div>
              <div className="text-[11px] text-brand/80 mt-2 font-mono">Пожизненно со всех платежей</div>
            </div>
          </div>
        </div>
      )}

      {/* IP Binding Modal */}
      {editingLicense && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="glass-panel p-8 rounded-3xl border border-white/15 max-w-md w-full shadow-2xl">
            <h3 className="text-xl font-bold text-white mb-2">Привязка IP-адреса сервера</h3>
            <p className="text-xs text-gray-400 mb-6">
              Укажите публичный IPv4 адрес VDS, на котором развернут ваш игровой сервер
            </p>

            {ipModalError && (
              <div className="mb-4 p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-red-400 text-xs flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{ipModalError}</span>
              </div>
            )}

            {ipModalSuccess && (
              <div className="mb-4 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
                <Check className="w-4 h-4 shrink-0" />
                <span>{ipModalSuccess}</span>
              </div>
            )}

            <form onSubmit={handleSaveIp} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  Название сервера
                </label>
                <input
                  type="text"
                  required
                  value={inputServerName}
                  onChange={(e) => setInputServerName(e.target.value)}
                  className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  IPv4 адрес сервера
                </label>
                <input
                  type="text"
                  required
                  value={inputIp}
                  onChange={(e) => setInputIp(e.target.value)}
                  placeholder="188.127.229.224"
                  className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                />
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-white/10">
                <button
                  type="button"
                  onClick={() => setEditingLicense(null)}
                  className="px-4 py-2 rounded-xl text-xs text-gray-400 hover:text-white"
                >
                  Отмена
                </button>
                <button
                  type="submit"
                  disabled={savingIp}
                  className="px-5 py-2.5 rounded-xl bg-brand hover:bg-brand-hover text-white text-xs font-bold shadow-neon-pink disabled:opacity-50"
                >
                  {savingIp ? 'Сохранение...' : 'Сохранить привязку'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New License Modal */}
      {newLicModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="glass-panel p-8 rounded-3xl border border-white/15 max-w-lg w-full shadow-2xl">
            <h3 className="text-xl font-bold text-white mb-2">Оформление новой лицензии</h3>
            <p className="text-xs text-gray-400 mb-6">
              Выберите подходящий тариф под масштабы вашего игрового проекта
            </p>

            <form onSubmit={handleCreateLicense} className="space-y-4">
              <div className="grid grid-cols-3 gap-3 mb-4">
                <div
                  onClick={() => setSelectedPlan('indie')}
                  className={`p-4 rounded-2xl border cursor-pointer text-center transition-all ${
                    selectedPlan === 'indie' ? 'border-brand bg-brand/10' : 'border-white/10 bg-surface-300'
                  }`}
                >
                  <div className="text-xs font-bold text-white">Инди</div>
                  <div className="text-[11px] text-gray-400 mt-1">128 слотов</div>
                  <div className="text-xs font-bold text-emerald-400 mt-2">Бесплатно</div>
                </div>

                <div
                  onClick={() => setSelectedPlan('business')}
                  className={`p-4 rounded-2xl border cursor-pointer text-center transition-all ${
                    selectedPlan === 'business' ? 'border-brand bg-brand/10 shadow-neon-pink' : 'border-white/10 bg-surface-300'
                  }`}
                >
                  <div className="text-xs font-bold text-brand">RP Проект</div>
                  <div className="text-[11px] text-gray-400 mt-1">512 слотов</div>
                  <div className="text-xs font-bold text-white mt-2">14 900 ₽</div>
                </div>

                <div
                  onClick={() => setSelectedPlan('enterprise')}
                  className={`p-4 rounded-2xl border cursor-pointer text-center transition-all ${
                    selectedPlan === 'enterprise' ? 'border-cyan-neon bg-cyan-500/10' : 'border-white/10 bg-surface-300'
                  }`}
                >
                  <div className="text-xs font-bold text-cyan-300">Enterprise</div>
                  <div className="text-[11px] text-gray-400 mt-1">1500+ слотов</div>
                  <div className="text-xs font-bold text-white mt-2">49 000 ₽</div>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  Название вашего сервера
                </label>
                <input
                  type="text"
                  required
                  value={newServerName}
                  onChange={(e) => setNewServerName(e.target.value)}
                  placeholder="Например: Moscow Night RP"
                  className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  IP сервера (можно указать позже)
                </label>
                <input
                  type="text"
                  value={newServerIp}
                  onChange={(e) => setNewServerIp(e.target.value)}
                  placeholder="0.0.0.0"
                  className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                />
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-white/10">
                <button
                  type="button"
                  onClick={() => setNewLicModalOpen(false)}
                  className="px-4 py-2 rounded-xl text-xs text-gray-400 hover:text-white"
                >
                  Отмена
                </button>
                <button
                  type="submit"
                  disabled={creatingLic}
                  className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white text-xs font-bold shadow-neon-pink disabled:opacity-50"
                >
                  {creatingLic ? 'Активация...' : 'Активировать лицензию'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New Invoice Modal */}
      {newInvoiceModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="glass-panel p-8 rounded-3xl border border-white/15 max-w-lg w-full shadow-2xl">
            <h3 className="text-xl font-bold text-white mb-2">Выставление счёта на оплату</h3>
            <p className="text-xs text-gray-400 mb-6">
              Выберите параметры лицензии и период оплаты
            </p>

            <form onSubmit={handleCreateInvoiceSubmit} className="space-y-5">
              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  Тарифный план
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <div
                    onClick={() => setInvoicePlan('business')}
                    className={`p-3 rounded-xl border cursor-pointer ${
                      invoicePlan === 'business' ? 'border-brand bg-brand/10' : 'border-white/10 bg-surface-300'
                    }`}
                  >
                    <div className="text-xs font-bold text-brand">RP Проект (512 слотов)</div>
                    <div className="text-xs font-mono text-white mt-1">14 900 ₽ / мес</div>
                  </div>

                  <div
                    onClick={() => setInvoicePlan('enterprise')}
                    className={`p-3 rounded-xl border cursor-pointer ${
                      invoicePlan === 'enterprise' ? 'border-cyan-neon bg-cyan-500/10' : 'border-white/10 bg-surface-300'
                    }`}
                  >
                    <div className="text-xs font-bold text-cyan-300">Enterprise (1500+ слотов)</div>
                    <div className="text-xs font-mono text-white mt-1">49 000 ₽ / лицензия</div>
                  </div>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  Период оплаты
                </label>
                <div className="grid grid-cols-3 gap-2">
                  <button
                    type="button"
                    onClick={() => setInvoicePeriod('monthly')}
                    className={`py-2 px-3 rounded-xl border text-xs font-mono font-bold ${
                      invoicePeriod === 'monthly' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    1 месяц
                  </button>

                  <button
                    type="button"
                    onClick={() => setInvoicePeriod('halfYear')}
                    className={`py-2 px-3 rounded-xl border text-xs font-mono font-bold ${
                      invoicePeriod === 'halfYear' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    6 мес (-15%)
                  </button>

                  <button
                    type="button"
                    onClick={() => setInvoicePeriod('year')}
                    className={`py-2 px-3 rounded-xl border text-xs font-mono font-bold ${
                      invoicePeriod === 'year' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    1 год (-30%)
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                  Способ оплаты
                </label>
                <div className="grid grid-cols-3 gap-2">
                  <button
                    type="button"
                    onClick={() => setInvoicePaymentMethod('card')}
                    className={`py-2 px-3 rounded-xl border text-xs font-semibold ${
                      invoicePaymentMethod === 'card' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    Карта РФ
                  </button>

                  <button
                    type="button"
                    onClick={() => setInvoicePaymentMethod('sbp')}
                    className={`py-2 px-3 rounded-xl border text-xs font-semibold ${
                      invoicePaymentMethod === 'sbp' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    СБП (QR)
                  </button>

                  <button
                    type="button"
                    onClick={() => setInvoicePaymentMethod('crypto')}
                    className={`py-2 px-3 rounded-xl border text-xs font-semibold ${
                      invoicePaymentMethod === 'crypto' ? 'border-brand bg-brand/10 text-white' : 'border-white/10 text-gray-400'
                    }`}
                  >
                    USDT / Крипта
                  </button>
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-white/10">
                <button
                  type="button"
                  onClick={() => setNewInvoiceModalOpen(false)}
                  className="px-4 py-2 rounded-xl text-xs text-gray-400 hover:text-white"
                >
                  Отмена
                </button>
                <button
                  type="submit"
                  disabled={creatingInvoice}
                  className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover text-white text-xs font-bold shadow-neon-pink disabled:opacity-50"
                >
                  {creatingInvoice ? 'Выставление...' : 'Выставить счёт'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
