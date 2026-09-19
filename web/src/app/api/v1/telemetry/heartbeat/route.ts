import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';

export async function POST(req: NextRequest) {
  try {
    const { licenseKey, players, maxPlayers, tickRate, memoryMb, fps } = await req.json();

    if (!licenseKey) {
      return NextResponse.json({ error: 'Ключ лицензии не указан' }, { status: 400 });
    }

    const forwardedFor = req.headers.get('x-forwarded-for');
    const realIp = req.headers.get('x-real-ip');
    const serverIp = (forwardedFor ? forwardedFor.split(',')[0].trim() : null) || realIp || '127.0.0.1';

    // Verify license
    let lics = await query('SELECT id, is_active, expires_at FROM portal_licenses WHERE license_key = ? LIMIT 1', [
      licenseKey.trim(),
    ]);

    if (lics.length === 0) {
      lics = await query('SELECT id, is_active, expires_at FROM portal_projects WHERE license_key = ? LIMIT 1', [
        licenseKey.trim(),
      ]);
    }

    if (lics.length === 0 || !lics[0].is_active) {
      return NextResponse.json({ error: 'Недействительная лицензия' }, { status: 403 });
    }
    if (lics[0].expires_at && (!Number.isFinite(new Date(lics[0].expires_at).getTime()) ||
        Date.now() > new Date(lics[0].expires_at).getTime())) {
      return NextResponse.json({ error: 'Срок действия лицензии истёк' }, { status: 403 });
    }

    await query(
      'INSERT INTO portal_telemetry (license_key, players, max_players, tick_rate, memory_mb, fps, server_ip) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [
        licenseKey.trim(),
        Number(players || 0),
        Number(maxPlayers || 1500),
        Number(tickRate || 60),
        Number(memoryMb || 0),
        Number(fps || 60),
        serverIp,
      ]
    );

    return NextResponse.json({
      acknowledged: true,
      timestamp: new Date().toISOString(),
    });
  } catch (err: any) {
    console.error('Telemetry heartbeat error:', err);
    return NextResponse.json({ error: 'Ошибка записи телеметрии' }, { status: 500 });
  }
}
