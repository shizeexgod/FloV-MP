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
  Share2,
  CreditCard,
  History,
  TrendingUp,
  Percent
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

export default function DashboardPage() {
  const router = useRouter();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [licenses, setLicenses] = useState<License[]>([]);
  const [loading, setLoading] = useState(true);

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

  useEffect(() => {
    loadDashboardData();
  }, []);

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
        setLicenses(licData.licenses || []);
      }
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
    } finally {
      setLoading(false);
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

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
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
              Здесь сводка по вашим лицензиям, привязке IP и подключению серверов FloV:MP
            </p>
          </div>
        </div>

        <button
          onClick={() => setNewLicModalOpen(true)}
          className="px-5 py-3 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white text-sm font-bold shadow-neon-pink flex items-center gap-2 transition-all"
        >
          <Plus className="w-4 h-4" />
          <span>Получить новую лицензию</span>
        </button>
      </div>

      {/* Quick Start Onboarding Tracker (like icsnotify.ru) */}
      <div className="glass-panel p-6 rounded-3xl border border-white/10 mb-8 shadow-glass">
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
      <div className="mb-12">
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

      {/* Guide & Launcher Builder Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-12">
        <div className="glass-panel p-8 rounded-3xl border border-white/10">
          <h3 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
            <Terminal className="w-5 h-5 text-brand" />
            <span>Инструкция по установке на сервер</span>
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

        {/* Custom Launcher Builder Widget */}
        <div className="glass-panel p-8 rounded-3xl border border-white/10">
          <h3 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
            <Layers className="w-5 h-5 text-cyan-neon" />
            <span>Сборщик брендированного лаунчера</span>
          </h3>
          <p className="text-xs text-gray-400 mb-6">
            Сгенерируйте готовый установщик лаунчера под имя вашего проекта
          </p>

          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                Название проекта в окне лаунчера
              </label>
              <input
                type="text"
                defaultValue="Держава Онлайн"
                className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                Фирменный акцентный цвет (HEX)
              </label>
              <div className="flex items-center gap-3">
                <input
                  type="color"
                  defaultValue="#ff3d8a"
                  className="w-10 h-10 rounded-xl bg-surface-300 border border-white/10 cursor-pointer"
                />
                <input
                  type="text"
                  defaultValue="#ff3d8a"
                  className="w-full px-4 py-2.5 bg-surface-300 border border-white/10 rounded-xl text-white text-sm font-mono"
                />
              </div>
            </div>

            <button
              onClick={() => alert('Сборка кастомного лаунчера отправлена в очередь билдера. Готовый exe-файл появится в течение 3 минут.')}
              className="w-full py-3 rounded-xl bg-surface-300 border border-cyan-500/40 text-cyan-300 hover:bg-cyan-500/10 font-bold text-xs flex items-center justify-center gap-2 transition-colors"
            >
              <ShieldCheck className="w-4 h-4" />
              <span>Скомпилировать лаунчер под проект</span>
            </button>
          </div>
        </div>
      </div>

      {/* Referral & Partnership Program (like icsnotify.ru) */}
      <div className="glass-panel p-8 rounded-3xl border border-white/10 mb-12 shadow-glass">
        <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
          <div className="space-y-2 max-w-2xl">
            <div className="flex items-center gap-2 text-brand font-mono text-xs font-bold uppercase tracking-wider">
              <Percent className="w-4 h-4" />
              <span>Партнёрская программа</span>
            </div>
            <h3 className="text-xl font-bold text-white">Приглашайте новые RP-проекты</h3>
            <p className="text-xs text-gray-400 leading-relaxed">
              Получайте <strong className="text-white">20%</strong> от каждой оплаты приглашенного вами проекта на свой баланс. Приглашенный проект получает персональную скидку <strong className="text-emerald-400">10%</strong> по вашему промокоду.
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
              <span>{copiedPromo ? 'Скопировано' : 'Копировать'}</span>
            </button>
          </div>
        </div>
      </div>

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
    </div>
  );
}
