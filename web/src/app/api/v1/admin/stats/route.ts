import { NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET() {
  const session = getSessionUser();
  if (!session || session.role !== 'admin') {
    return NextResponse.json({ error: 'Доступ разрешен только администраторам платформы' }, { status: 403 });
  }

  const users = await query('SELECT id, email, username, role, telegram, created_at FROM portal_users ORDER BY id DESC');
  const licenses = await query('SELECT id, user_id, license_key, server_name, bound_ip, plan, max_players, is_active, expires_at, created_at, last_verified_at FROM portal_licenses ORDER BY id DESC');
  const invoices = await query('SELECT id, user_id, license_id, amount_rub, plan, status, created_at, paid_at FROM portal_invoices ORDER BY id DESC');

  const totalRevenue = invoices
    .filter((i: any) => i.status === 'paid')
    .reduce((sum: number, i: any) => sum + (Number(i.amount_rub) || 0), 0);

  const activeLicenses = licenses.filter((l: any) => l.is_active && new Date(l.expires_at).getTime() > Date.now());

  return NextResponse.json({
    metrics: {
      totalUsers: users.length,
      totalLicenses: licenses.length,
      activeServers: activeLicenses.length,
      totalRevenueRub: totalRevenue,
    },
    users,
    licenses,
    invoices,
  });
}
