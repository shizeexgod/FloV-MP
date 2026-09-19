import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { createSignedLicenseLease, signLicensePayload, isValidLicenseKeyFormat } from '@/lib/license';

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
    if (!isValidLicenseKeyFormat(String(licenseKey))) {
      return NextResponse.json({ valid: false, reason: 'Некорректный формат лицензионного ключа' }, { status: 400 });
    }

    // For an IP-bound entitlement use the transport address supplied by the
    // hosting proxy. `serverIp` is only metadata: it is sent by the caller and
    // must never be accepted as proof of where that caller is running.
    const forwardedFor = req.headers.get('x-forwarded-for');
    const realIp = req.headers.get('x-real-ip');
    const requestIp = (req as NextRequest & { ip?: string }).ip;
    const clientIp = (forwardedFor ? forwardedFor.split(',')[0].trim() : null) || realIp || requestIp || '127.0.0.1';

    const licenses = await query(
      'SELECT * FROM portal_licenses WHERE license_key = ? LIMIT 1',
      [licenseKey.trim()]
    );

    let lic: any = licenses[0];
    let licenseSource: 'license' | 'project' = 'license';
    if (!lic) {
      const projects = await query(
        'SELECT * FROM portal_projects WHERE license_key = ? LIMIT 1',
        [licenseKey.trim()]
      );
      if (projects.length > 0) {
        const project = projects[0] as any;
        lic = {
          ...project,
          server_name: project.name,
          bound_ip: '0.0.0.0',
          max_players: project.max_players,
        };
        licenseSource = 'project';
      }
    }

    if (!lic) {
      return NextResponse.json(
        { valid: false, reason: 'Лицензия не найдена в базе FloV:MP' },
        { status: 403 }
      );
    }

    // Check active
    if (!lic.is_active) {
      return NextResponse.json(
        { valid: false, reason: 'Лицензия приостановлена администратором платформы' },
        { status: 403 }
      );
    }

    const expiry = new Date(lic.expires_at).getTime();
    if (!Number.isFinite(expiry) || Date.now() > expiry) {
      return NextResponse.json(
        { valid: false, reason: 'Срок действия лицензии истёк. Продлите подписку в ЛК' },
        { status: 403 }
      );
    }

    // Check IP binding
    const boundIp = (lic.bound_ip || '0.0.0.0').trim();
    if (boundIp !== '0.0.0.0' && boundIp !== '127.0.0.1') {
      if (clientIp.trim() !== boundIp) {
        return NextResponse.json(
          {
            valid: false,
            reason: `IP сервера (${serverIp || clientIp}) не совпадает с привязанным в ЛК (${boundIp})`,
          },
          { status: 403 }
        );
      }
    } else if (licenseSource === 'license' && boundIp === '0.0.0.0' && clientIp && clientIp !== '127.0.0.1') {
      // Auto-bind on first activation if unbound
      await query('UPDATE portal_licenses SET bound_ip = ? WHERE id = ?', [clientIp, lic.id]);
    }

    // Update last_verified_at
    if (licenseSource === 'license') {
      await query('UPDATE portal_licenses SET last_verified_at = CURRENT_TIMESTAMP WHERE id = ?', [lic.id]);
    }

    const validUntil = new Date(Math.min(expiry, Date.now() + 24 * 3600 * 1000)).toISOString();
    const lease = createSignedLicenseLease({ licenseKey: lic.license_key, validUntil });
    const responsePayload = {
      valid: true,
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
      leasePayloadB64: lease.leasePayloadB64,
      leaseSignature: lease.leaseSignature,
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
