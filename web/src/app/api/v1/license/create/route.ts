import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { generateLicenseKey, createSignedLicenseFlv } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const { plan, serverName, boundIp, customSlots, days: customDays } = await req.json();

    const validPlans: Record<string, { maxPlayers: number; days: number }> = {
      indie: { maxPlayers: 128, days: 30 },
      business: { maxPlayers: 1500, days: 30 },
      enterprise: { maxPlayers: 5000, days: 365 },
    };

    const selectedPlan = (plan && validPlans[plan]) ? plan : 'enterprise';
    const planConfig = validPlans[selectedPlan];
    const maxPlayers = customSlots ? Math.max(1, Number(customSlots)) : planConfig.maxPlayers;
    const durationDays = customDays ? Math.max(1, Number(customDays)) : planConfig.days;

    const key = generateLicenseKey('FLV');
    const expiresAt = new Date(Date.now() + durationDays * 24 * 3600 * 1000)
      .toISOString()
      .slice(0, 19)
      .replace('T', ' ');

    const sName = serverName?.trim() || `${session.username} RP Server`;

    const res: any = await query(
      'INSERT INTO portal_licenses (user_id, license_key, server_name, bound_ip, plan, max_players, expires_at) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [
        session.userId,
        key,
        sName,
        boundIp?.trim() || '0.0.0.0',
        selectedPlan,
        maxPlayers,
        expiresAt,
      ]
    );

    // Генерируем официальный криптографический license.flv
    const { flvJson } = createSignedLicenseFlv({
      licenseKey: key,
      project: sName,
      issuedTo: session.username,
      plan: selectedPlan,
      maxPlayers,
      maxServers: selectedPlan === 'enterprise' ? 10 : 3,
      days: durationDays,
    });

    return NextResponse.json({
      success: true,
      licenseId: res.insertId,
      licenseKey: key,
      plan: selectedPlan,
      maxPlayers,
      expiresAt,
      flvContent: flvJson,
      message: 'Лицензия успешно создана, подписана RSA-2048 и готова к скачиванию!',
    });
  } catch (err: any) {
    console.error('Create license error:', err);
    return NextResponse.json({ error: 'Ошибка генерации лицензии' }, { status: 500 });
  }
}
