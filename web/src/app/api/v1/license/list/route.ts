import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET() {
  const session = getSessionUser();
  if (!session) {
    return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
  }

  const licenses = await query(
    'SELECT id, license_key, server_name, bound_ip, plan, max_players, is_active, expires_at, created_at, last_verified_at FROM portal_licenses WHERE user_id = ? ORDER BY id DESC',
    [session.userId]
  );

  return NextResponse.json({
    licenses,
  });
}
