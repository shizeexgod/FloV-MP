'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  Shield,
  Key,
  Users,
  CreditCard,
  Server,
  RefreshCw,
  Power,
  Calendar,
  Check,
  AlertCircle,
  Clock,
  ArrowUpRight,
  TrendingUp,
  Activity
} from 'lucide-react';

interface MetricOverview {
  totalUsers: number;
  totalLicenses: number;
  activeServers: number;
  totalRevenueRub: number;
}

export default function AdminPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [metrics, setMetrics] = useState<MetricOverview | null>(null);
  const [licenses, setLicenses] = useState<any[]>([]);
  const [users, setUsers] = useState<any[]>([]);
  const [invoices, setInvoices] = useState<any[]>([]);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  useEffect(() => {
    loadAdminData();
  }, []);

  const loadAdminData = async () => {
    setLoading(true);
    try {
      const res = await fetch('/api/v1/admin/stats');
      if (!res.ok) {
        if (res.status === 403) {
          alert('Доступ запрещен: требуется аккаунт администратора');
          router.push('/dashboard');
          return;
        }
        router.push('/auth/login');
        return;
      }
      const data = await res.json();
      setMetrics(data.metrics);
      setLicenses(data.licenses || []);
      setUsers(data.users || []);
      setInvoices(data.invoices || []);
    } catch (err) {
      console.error('Failed to load admin stats:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleToggleLicense = async (licenseId: number, currentActive: number) => {
    try {
      const res = await fetch('/api/v1/admin/licenses/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ licenseId, isActive: !currentActive }),
      });
      const data = await res.json();
      if (res.ok) {
        setActionMessage(data.message);
        setTimeout(() => setActionMessage(null), 3000);
        loadAdminData();
      }
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleExtendLicense = async (licenseId: number, days: number) => {
    try {
      const res = await fetch('/api/v1/admin/licenses/extend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ licenseId, days }),
      });
      const data = await res.json();
      if (res.ok) {
        setActionMessage(data.message);
        setTimeout(() => setActionMessage(null), 3000);
        loadAdminData();
      }
    } catch (err: any) {
      alert(err.message);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[70vh] flex flex-col items-center justify-center gap-3">
        <RefreshCw className="w-8 h-8 text-brand animate-spin" />
        <p className="text-gray-400 text-sm">Загрузка панели администратора...</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Admin Header */}
      <div className="glass-panel p-8 rounded-3xl border border-white/10 mb-8 flex flex-col md:flex-row items-start md:items-center justify-between gap-6 shadow-glass">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-red-500/20 text-red-400 border border-red-500/30 text-xs font-mono font-bold uppercase tracking-wider mb-2">
            <Shield className="w-3.5 h-3.5" />
            <span>FloV:MP Master Admin Panel</span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-black text-white">
            Управление платформой и реестром серверов
          </h1>
          <p className="text-xs text-gray-400 mt-1">
            Контроль выданных лицензий, клиентов, платежей и статусов серверов
          </p>
        </div>

        <button
          onClick={loadAdminData}
          className="px-4 py-2.5 rounded-xl bg-surface-300 border border-white/10 hover:border-brand/40 text-xs text-gray-300 hover:text-white font-bold flex items-center gap-2 transition-colors"
        >
          <RefreshCw className="w-4 h-4 text-brand" />
          <span>Обновить данные</span>
        </button>
      </div>

      {actionMessage && (
        <div className="mb-6 p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-sm flex items-center gap-2">
          <Check className="w-4 h-4" />
          <span>{actionMessage}</span>
        </div>
      )}

      {/* Metrics Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 mb-10">
        <div className="glass-panel p-6 rounded-2xl border border-white/10">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-gray-400 uppercase font-mono tracking-wider">Всего клиентов</span>
            <Users className="w-5 h-5 text-blue-400" />
          </div>
          <div className="text-3xl font-black text-white font-mono">{metrics?.totalUsers || 0}</div>
        </div>

        <div className="glass-panel p-6 rounded-2xl border border-white/10">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-gray-400 uppercase font-mono tracking-wider">Выдано лицензий</span>
            <Key className="w-5 h-5 text-brand" />
          </div>
          <div className="text-3xl font-black text-white font-mono">{metrics?.totalLicenses || 0}</div>
        </div>

        <div className="glass-panel p-6 rounded-2xl border border-white/10">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-gray-400 uppercase font-mono tracking-wider">Активных узлов</span>
            <Server className="w-5 h-5 text-emerald-400" />
          </div>
          <div className="text-3xl font-black text-emerald-400 font-mono">{metrics?.activeServers || 0}</div>
        </div>

        <div className="glass-panel p-6 rounded-2xl border border-white/10">
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-gray-400 uppercase font-mono tracking-wider">Выручка платформы</span>
            <TrendingUp className="w-5 h-5 text-cyan-neon" />
          </div>
          <div className="text-3xl font-black text-white font-mono">
            {(metrics?.totalRevenueRub || 0).toLocaleString('ru-RU')} ₽
          </div>
        </div>
      </div>

      {/* Licenses Management Table */}
      <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 mb-10 shadow-glass">
        <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
          <Key className="w-5 h-5 text-brand" />
          <span>Все выданные лицензии ({licenses.length})</span>
        </h2>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs font-mono">
            <thead>
              <tr className="border-b border-white/10 text-gray-400 uppercase">
                <th className="py-3 px-2">ID</th>
                <th className="py-3 px-2">Сервер / Проект</th>
                <th className="py-3 px-2">Ключ</th>
                <th className="py-3 px-2">Тариф</th>
                <th className="py-3 px-2">IP Привязка</th>
                <th className="py-3 px-2">Статус</th>
                <th className="py-3 px-2">Истекает</th>
                <th className="py-3 px-2 text-right">Действия</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5 text-gray-300">
              {licenses.map((lic) => {
                const isExpired = new Date(lic.expires_at).getTime() < Date.now();
                return (
                  <tr key={lic.id} className="hover:bg-white/5 transition-colors">
                    <td className="py-3 px-2 text-gray-400">#{lic.id}</td>
                    <td className="py-3 px-2 font-bold text-white font-sans">{lic.server_name}</td>
                    <td className="py-3 px-2 text-brand font-bold">{lic.license_key}</td>
                    <td className="py-3 px-2 uppercase">{lic.plan} ({lic.max_players} сл.)</td>
                    <td className="py-3 px-2">{lic.bound_ip}</td>
                    <td className="py-3 px-2">
                      <span
                        className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                          lic.is_active && !isExpired
                            ? 'bg-emerald-500/20 text-emerald-400'
                            : 'bg-red-500/20 text-red-400'
                        }`}
                      >
                        {lic.is_active && !isExpired ? 'АКТИВНА' : 'ПРИОСТАНОВЛЕНА'}
                      </span>
                    </td>
                    <td className="py-3 px-2 text-gray-400">
                      {new Date(lic.expires_at).toLocaleDateString('ru-RU')}
                    </td>
                    <td className="py-3 px-2 text-right space-x-2">
                      <button
                        onClick={() => handleToggleLicense(lic.id, lic.is_active)}
                        className={`px-2.5 py-1 rounded-lg text-[10px] font-bold transition-colors ${
                          lic.is_active
                            ? 'bg-red-500/20 text-red-400 hover:bg-red-500/30'
                            : 'bg-emerald-500/20 text-emerald-400 hover:bg-emerald-500/30'
                        }`}
                        title={lic.is_active ? 'Приостановить' : 'Активировать'}
                      >
                        {lic.is_active ? 'Заморозить' : 'Активировать'}
                      </button>
                      <button
                        onClick={() => handleExtendLicense(lic.id, 30)}
                        className="px-2.5 py-1 rounded-lg bg-white/10 hover:bg-white/20 text-white text-[10px] font-bold transition-colors"
                        title="Продлить на 30 дней"
                      >
                        +30 дн.
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Invoices History Table */}
      <div className="glass-panel p-6 sm:p-8 rounded-3xl border border-white/10 shadow-glass">
        <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
          <CreditCard className="w-5 h-5 text-cyan-neon" />
          <span>История счетов и платежей ({invoices.length})</span>
        </h2>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs font-mono">
            <thead>
              <tr className="border-b border-white/10 text-gray-400 uppercase">
                <th className="py-3 px-2">ID</th>
                <th className="py-3 px-2">Клиент ID</th>
                <th className="py-3 px-2">Сумма</th>
                <th className="py-3 px-2">Тариф</th>
                <th className="py-3 px-2">Способ</th>
                <th className="py-3 px-2">Статус</th>
                <th className="py-3 px-2">Дата создания</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5 text-gray-300">
              {invoices.map((inv) => (
                <tr key={inv.id} className="hover:bg-white/5 transition-colors">
                  <td className="py-3 px-2 text-gray-400">#{inv.id}</td>
                  <td className="py-3 px-2">User #{inv.user_id}</td>
                  <td className="py-3 px-2 font-bold text-white font-mono">
                    {Number(inv.amount_rub).toLocaleString('ru-RU')} ₽
                  </td>
                  <td className="py-3 px-2 uppercase">{inv.plan}</td>
                  <td className="py-3 px-2 uppercase">{inv.payment_method}</td>
                  <td className="py-3 px-2">
                    <span
                      className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                        inv.status === 'paid'
                          ? 'bg-emerald-500/20 text-emerald-400'
                          : 'bg-amber-500/20 text-amber-400'
                      }`}
                    >
                      {inv.status === 'paid' ? 'ОПЛАЧЕН' : 'ОЖИДАЕТ'}
                    </span>
                  </td>
                  <td className="py-3 px-2 text-gray-400">
                    {new Date(inv.created_at).toLocaleDateString('ru-RU')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
