import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { signLicensePayload } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    let body: any;
    try {
      body = await req.json();
    } catch {
      return NextResponse.json(
        { valid: false, reason: 'Некорректный формат JSON запроса' },
        { status: 400 }
      );
    }
    const { licenseKey, serverIp, version, slots } = body || {};

    if (!licenseKey) {
      return NextResponse.json(
        { valid: false, reason: 'Лицензионный ключ не указан' },
        { status: 400 }
      );
    }

    // Determine caller remote IP if serverIp not passed
    const forwardedFor = req.headers.get('x-forwarded-for');
    const realIp = req.headers.get('x-real-ip');
    const clientIp = (forwardedFor ? forwardedFor.split(',')[0].trim() : null) || realIp || serverIp || '127.0.0.1';

    const licenses = await query(
      'SELECT * FROM portal_licenses WHERE license_key = ? LIMIT 1',
      [licenseKey.trim()]
    );

    if (licenses.length === 0) {
      return NextResponse.json(
        { valid: false, reason: 'Лицензия не найдена в базе FloV:MP' },
        { status: 403 }
      );
    }

    const lic = licenses[0];

    // Check active
    if (!lic.is_active) {
      return NextResponse.json(
        { valid: false, reason: 'Лицензия приостановлена администратором платформы' },
        { status: 403 }
      );
    }

    // Check expiration — with a 7-day grace window so a late renewal does not
    // hard-stop a live server. The game server also caches the signed response
    // and may keep running up to `graceHours` if the portal is unreachable.
    const GRACE_MS = 7 * 24 * 3600 * 1000;
    const expiry = new Date(lic.expires_at).getTime();
    const inGrace = Date.now() > expiry && Date.now() <= expiry + GRACE_MS;
    if (Date.now() > expiry + GRACE_MS) {
      return NextResponse.json(
        { valid: false, reason: 'Срок действия лицензии истёк более 7 дней назад. Продлите подписку в ЛК' },
        { status: 403 }
      );
    }

    // Check IP binding
    const boundIp = (lic.bound_ip || '0.0.0.0').trim();
    if (boundIp !== '0.0.0.0' && boundIp !== '127.0.0.1') {
      const match1 = serverIp && serverIp.trim() === boundIp;
      const match2 = clientIp && clientIp.trim() === boundIp;
      if (!match1 && !match2) {
        return NextResponse.json(
          {
            valid: false,
            reason: `IP сервера (${serverIp || clientIp}) не совпадает с привязанным в ЛК (${boundIp})`,
          },
          { status: 403 }
        );
      }
    } else if (boundIp === '0.0.0.0' && clientIp && clientIp !== '127.0.0.1') {
      // Auto-bind on first activation if unbound
      await query('UPDATE portal_licenses SET bound_ip = ? WHERE id = ?', [clientIp, lic.id]);
    }

    // Update last_verified_at
    await query('UPDATE portal_licenses SET last_verified_at = CURRENT_TIMESTAMP WHERE id = ?', [lic.id]);

    const responsePayload = {
      valid: true,
      grace: inGrace,
      graceHours: 168,
      graceUntil: new Date(expiry + GRACE_MS).toISOString(),
      licenseKey: lic.license_key,
      serverName: lic.server_name,
      plan: lic.plan,
      maxPlayers: lic.max_players,
      expiresAt: lic.expires_at,
      boundIp: boundIp === '0.0.0.0' ? clientIp : boundIp,
      features: {
        voice3D: true,
        antiCheat: true,
        customDlcStreaming: true,
        fastDlCdn: true,
        unlimitedEntities: lic.plan === 'enterprise',
      },
      verifiedAt: new Date().toISOString(),
    };

    const signature = signLicensePayload(responsePayload);

    return NextResponse.json({
      ...responsePayload,
      signature,
    });
  } catch (err: any) {
    console.error('License verify error:', err);
    return NextResponse.json(
      { valid: false, reason: 'Ошибка валидатора лицензий' },
      { status: 500 }
    );
  }
}
