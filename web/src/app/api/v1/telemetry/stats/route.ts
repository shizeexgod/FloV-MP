import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET(req: NextRequest) {
  const session = getSessionUser();
  if (!session) {
    return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
  }

  const { searchParams } = new URL(req.url);
  const licenseKey = searchParams.get('key');

  if (!licenseKey) {
    return NextResponse.json({ error: 'Параметр key обязателен' }, { status: 400 });
  }

  // Verify ownership
  const lics = await query('SELECT id FROM portal_licenses WHERE license_key = ? AND user_id = ? LIMIT 1', [
    licenseKey.trim(),
    session.userId,
  ]);

  if (lics.length === 0 && !['owner','admin'].includes(session.role)) {
    return NextResponse.json({ error: 'Доступ запрещен' }, { status: 403 });
  }

  const points = await query(
    'SELECT players, max_players, tick_rate, memory_mb, fps, server_ip, recorded_at FROM portal_telemetry WHERE license_key = ? ORDER BY id DESC LIMIT 30',
    [licenseKey.trim()]
  );

  return NextResponse.json({
    licenseKey,
    stats: points.reverse(),
  });
}
