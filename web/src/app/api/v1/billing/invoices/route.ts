import { NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET() {
  const session = getSessionUser();
  if (!session) {
    return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
  }

  const invoices = await query(
    'SELECT id, user_id, license_id, amount_rub, plan, payment_method, payment_id, status, created_at, paid_at FROM portal_invoices WHERE user_id = ? ORDER BY id DESC',
    [session.userId]
  );

  return NextResponse.json({ invoices });
}
