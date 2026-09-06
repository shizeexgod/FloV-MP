import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { generateLicenseKey } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const { plan, serverName, boundIp } = await req.json();

    const validPlans: Record<string, { maxPlayers: number; days: number }> = {
      indie: { maxPlayers: 128, days: 30 },
      business: { maxPlayers: 512, days: 30 },
      enterprise: { maxPlayers: 1500, days: 365 },
    };

    const selectedPlan = (plan && validPlans[plan]) ? plan : 'indie';
    const planConfig = validPlans[selectedPlan];

    const key = generateLicenseKey('FLV');
    const expiresAt = new Date(Date.now() + planConfig.days * 24 * 3600 * 1000)
      .toISOString()
      .slice(0, 19)
      .replace('T', ' ');

    const res: any = await query(
      'INSERT INTO portal_licenses (user_id, license_key, server_name, bound_ip, plan, max_players, expires_at) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [
        session.userId,
        key,
        serverName?.trim() || `${session.username} RP Server`,
        boundIp?.trim() || '0.0.0.0',
        selectedPlan,
        planConfig.maxPlayers,
        expiresAt,
      ]
    );

    return NextResponse.json({
      success: true,
      licenseId: res.insertId,
      licenseKey: key,
      plan: selectedPlan,
      maxPlayers: planConfig.maxPlayers,
      expiresAt,
      message: 'Лицензия успешно сгенерирована и активирована!',
    });
  } catch (err: any) {
    console.error('Create license error:', err);
    return NextResponse.json({ error: 'Ошибка генерации лицензии' }, { status: 500 });
  }
}
