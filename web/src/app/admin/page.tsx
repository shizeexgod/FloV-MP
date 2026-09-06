'use client';

import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  CreditCard,
  KeyRound,
  RefreshCw,
  Server,
  ShieldAlert,
  TrendingUp,
  Users,
  Wallet,
} from 'lucide-react';
import { AuroraBlobs, Badge, Spinner, useToast } from '@/components/ui';

interface MetricOverview {
  totalUsers: number;
  totalLicenses: number;
  activeServers: number;
  totalRevenueRub: number;
}

const fmt = (n: number) => Number(n || 0).toLocaleString('ru-RU');
const date = (s: string) => new Date(s).toLocaleDateString('ru-RU');

export default function AdminPage() {
  const router = useRouter();
  const { show, node } = useToast();
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [metrics, setMetrics] = useState<MetricOverview | null>(null);
  const [licenses, setLicenses] = useState<any[]>([]);
  const [users, setUsers] = useState<any[]>([]);
  const [invoices, setInvoices] = useState<any[]>([]);

  useEffect(() => {
    load();
  }, []);

  const load = async () => {
    setLoading(true);
    try {
      const res = await fetch('/api/v1/admin/stats');
      if (!res.ok) {
        if (res.status === 403) {
          show('Доступ запрещён: требуется аккаунт администратора', 'error');
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
    } catch {
      show('Не удалось загрузить данные платформы', 'error');
    } finally {
      setLoading(false);
    }
  };

  const toggleLicense = async (licenseId: number, currentActive: number) => {
    setBusyId(licenseId);
    try {
      const res = await fetch('/api/v1/admin/licenses/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ licenseId, isActive: !currentActive }),
      });
      const data = await res.json();
      if (res.ok) {
        show(data.message);
        load();
      } else {
        show(data.error || 'Ошибка', 'error');
      }
    } finally {
      setBusyId(null);
    }
  };

  const extendLicense = async (licenseId: number, days: number) => {
    setBusyId(licenseId);
    try {
      const res = await fetch('/api/v1/admin/licenses/extend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ licenseId, days }),
      });
      const data = await res.json();
      if (res.ok) {
        show(data.message);
        load();
      } else {
        show(data.error || 'Ошибка', 'error');
      }
    } finally {
      setBusyId(null);
    }
  };

  if (loading) {
    return (
      <div className="flex min-h-[70vh] flex-col items-center justify-center gap-3 text-brand">
        <Spinner className="h-8 w-8" />
        <p className="text-sm text-slate-400">Загрузка панели администратора…</p>
      </div>
    );
  }

  const cards = [
    { label: 'Общий доход', value: `${fmt(metrics?.totalRevenueRub || 0)} ₽`, icon: TrendingUp, tone: 'text-brand', ring: 'border-brand/40 bg-brand/10' },
    { label: 'Всего серверов', value: fmt(metrics?.totalLicenses || 0), icon: KeyRound, tone: 'text-cyber', ring: 'border-cyber/40 bg-cyber/10' },
    { label: 'Активные лицензии', value: fmt(metrics?.activeServers || 0), icon: Server, tone: 'text-emeraldx', ring: 'border-emeraldx/40 bg-emeraldx/10' },
    { label: 'Всего клиентов', value: fmt(metrics?.totalUsers || 0), icon: Users, tone: 'text-violetx', ring: 'border-violetx/40 bg-violetx/10' },
  ];

  return (
    <div className="relative mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      <AuroraBlobs />
      {node}

      {/* Header */}
      <div className="relative glass-panel card-edge mb-8 flex flex-col gap-5 rounded-3xl p-7 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
        <div>
          <span className="inline-flex items-center gap-2 rounded-full border border-red-500/30 bg-red-500/10 px-3 py-1 font-mono text-[10px] font-bold uppercase tracking-wider text-red-400">
            <ShieldAlert className="h-3.5 w-3.5" />
            Master Admin Panel
          </span>
          <h1 className="mt-3 text-2xl font-black text-white sm:text-3xl">
            Управление платформой и реестром серверов
          </h1>
          <p className="mt-1 text-xs text-slate-400">
            Контроль лицензий, клиентов, платежей и статусов узлов
          </p>
        </div>
        <button onClick={load} className="btn btn-ghost h-10 px-4 text-xs font-semibold">
          <RefreshCw className="h-4 w-4 text-brand" />
          Обновить
        </button>
      </div>

      {/* Metrics */}
      <div className="relative mb-10 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
        {cards.map((c) => (
          <div key={c.label} className="glass card-edge rounded-2xl p-6">
            <div className="flex items-center justify-between">
              <span className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{c.label}</span>
              <span className={`flex h-9 w-9 items-center justify-center rounded-xl border ${c.ring} ${c.tone}`}>
                <c.icon className="h-4 w-4" />
              </span>
            </div>
            <div className="mt-3 font-mono text-3xl font-black text-white">{c.value}</div>
          </div>
        ))}
      </div>

      {/* Licenses */}
      <div className="relative glass-panel card-edge mb-10 rounded-3xl p-6 shadow-glass sm:p-8">
        <h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-white">
          <KeyRound className="h-5 w-5 text-brand" />
          Все выданные лицензии
          <span className="font-mono text-xs font-normal text-slate-500">({licenses.length})</span>
        </h2>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-left text-xs">
            <thead>
              <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                <th className="pb-3 pr-3 font-semibold">ID</th>
                <th className="pb-3 pr-3 font-semibold">Сервер / проект</th>
                <th className="pb-3 pr-3 font-semibold">Ключ</th>
                <th className="pb-3 pr-3 font-semibold">Тариф</th>
                <th className="pb-3 pr-3 font-semibold">IP</th>
                <th className="pb-3 pr-3 font-semibold">Статус</th>
                <th className="pb-3 pr-3 font-semibold">Истекает</th>
                <th className="pb-3 pr-3 text-right font-semibold">Действия</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/[0.05]">
              {licenses.map((lic) => {
                const expired = new Date(lic.expires_at).getTime() < Date.now();
                const active = lic.is_active && !expired;
                return (
                  <tr key={lic.id} className="text-slate-300 transition-colors hover:bg-white/[0.03]">
                    <td className="py-3 pr-3 font-mono text-slate-500">#{lic.id}</td>
                    <td className="py-3 pr-3 font-semibold text-white">{lic.server_name}</td>
                    <td className="py-3 pr-3 font-mono text-brand">{lic.license_key}</td>
                    <td className="py-3 pr-3 font-mono uppercase">
                      {lic.plan}
                      <span className="text-slate-500"> · {lic.max_players}</span>
                    </td>
                    <td className="py-3 pr-3 font-mono">{lic.bound_ip}</td>
                    <td className="py-3 pr-3">
                      <Badge tone={active ? 'emerald' : 'red'}>{active ? 'Активна' : 'Приостановлена'}</Badge>
                    </td>
                    <td className="py-3 pr-3 font-mono text-slate-400">{date(lic.expires_at)}</td>
                    <td className="py-3 pr-3">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => toggleLicense(lic.id, lic.is_active)}
                          disabled={busyId === lic.id}
                          className={`rounded-lg px-2.5 py-1 text-[10px] font-bold transition-colors disabled:opacity-40 ${
                            lic.is_active
                              ? 'bg-red-500/15 text-red-400 hover:bg-red-500/25'
                              : 'bg-emeraldx/15 text-emeraldx hover:bg-emeraldx/25'
                          }`}
                        >
                          {lic.is_active ? 'Заморозить' : 'Разморозить'}
                        </button>
                        {[30, 90, 365].map((d) => (
                          <button
                            key={d}
                            onClick={() => extendLicense(lic.id, d)}
                            disabled={busyId === lic.id}
                            className="rounded-lg bg-white/5 px-2 py-1 text-[10px] font-bold text-white transition-colors hover:bg-white/10 disabled:opacity-40"
                          >
                            +{d}
                          </button>
                        ))}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Users + Transactions */}
      <div className="relative grid grid-cols-1 gap-8 lg:grid-cols-2">
        <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
          <h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-white">
            <Users className="h-5 w-5 text-violetx" />
            Клиенты
            <span className="font-mono text-xs font-normal text-slate-500">({users.length})</span>
          </h2>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px] text-left text-xs">
              <thead>
                <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                  <th className="pb-3 pr-3 font-semibold">ID</th>
                  <th className="pb-3 pr-3 font-semibold">Логин</th>
                  <th className="pb-3 pr-3 font-semibold">Email</th>
                  <th className="pb-3 pr-3 font-semibold">Роль</th>
                  <th className="pb-3 pr-3 font-semibold">Создан</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/[0.05]">
                {users.map((u) => (
                  <tr key={u.id} className="text-slate-300 transition-colors hover:bg-white/[0.03]">
                    <td className="py-3 pr-3 font-mono text-slate-500">#{u.id}</td>
                    <td className="py-3 pr-3 font-semibold text-white">{u.username}</td>
                    <td className="py-3 pr-3 font-mono text-slate-400">{u.email}</td>
                    <td className="py-3 pr-3">
                      <Badge tone={u.role === 'admin' ? 'red' : 'slate'}>{u.role}</Badge>
                    </td>
                    <td className="py-3 pr-3 font-mono text-slate-400">{u.created_at ? date(u.created_at) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
          <h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-white">
            <CreditCard className="h-5 w-5 text-cyber" />
            Реестр транзакций
            <span className="font-mono text-xs font-normal text-slate-500">({invoices.length})</span>
          </h2>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[460px] text-left text-xs">
              <thead>
                <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                  <th className="pb-3 pr-3 font-semibold">ID</th>
                  <th className="pb-3 pr-3 font-semibold">Клиент</th>
                  <th className="pb-3 pr-3 font-semibold">Сумма</th>
                  <th className="pb-3 pr-3 font-semibold">Тариф</th>
                  <th className="pb-3 pr-3 font-semibold">Статус</th>
                  <th className="pb-3 pr-3 font-semibold">Дата</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/[0.05]">
                {invoices.map((inv) => (
                  <tr key={inv.id} className="text-slate-300 transition-colors hover:bg-white/[0.03]">
                    <td className="py-3 pr-3 font-mono text-slate-500">#{inv.id}</td>
                    <td className="py-3 pr-3 font-mono">User&nbsp;#{inv.user_id}</td>
                    <td className="py-3 pr-3 font-mono font-bold text-white">{fmt(inv.amount_rub)} ₽</td>
                    <td className="py-3 pr-3 font-mono uppercase">{inv.plan}</td>
                    <td className="py-3 pr-3">
                      <Badge tone={inv.status === 'paid' ? 'emerald' : 'amber'}>
                        {inv.status === 'paid' ? 'Оплачен' : 'Ожидает'}
                      </Badge>
                    </td>
                    <td className="py-3 pr-3 font-mono text-slate-400">{date(inv.created_at)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {invoices.length === 0 && (
            <p className="py-8 text-center text-xs text-slate-500">Транзакций пока нет.</p>
          )}
        </div>
      </div>

      <div className="relative mt-8 flex items-center gap-2 font-mono text-[11px] text-slate-600">
        <Wallet className="h-3.5 w-3.5" />
        Все операции журналируются · owner@flovmp.ru
      </div>
    </div>
  );
}
