import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { createSignedLicenseFlv, isValidLicenseKeyFormat } from '@/lib/license';

export const dynamic = 'force-dynamic';

/**
 * GET /api/v1/licenses/download-by-key?key=FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX
 * Автоматическое получение криптографического license.flv по ключу лицензии (для install.sh).
 */
export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const key = searchParams.get('key')?.trim();

    if (!key) {
      return NextResponse.json({ success: false, error: 'Параметр key обязателен' }, { status: 400 });
    }
    if (!isValidLicenseKeyFormat(key)) {
      return NextResponse.json({ success: false, error: 'Некорректный формат лицензионного ключа' }, { status: 400 });
    }

    // 1. Поиск в portal_licenses
    let rows: any = await query('SELECT * FROM portal_licenses WHERE license_key = ? LIMIT 1', [key]);
    let lic = rows && rows.length > 0 ? rows[0] : null;

    // 2. Если не найдено в licenses, ищем в portal_projects
    if (!lic) {
      const projRows: any = await query('SELECT * FROM portal_projects WHERE license_key = ? LIMIT 1', [key]);
      if (projRows && projRows.length > 0) {
        const p = projRows[0];
        lic = {
          license_key: p.license_key,
          server_name: p.name,
          plan: p.plan,
          max_players: p.max_players,
          is_active: p.is_active,
          expires_at: p.expires_at,
          username: 'Licensee',
        };
      }
    }

    if (!lic) {
      return NextResponse.json({ success: false, error: 'Лицензия с указанным ключом не найдена' }, { status: 404 });
    }

    if (!lic.is_active) {
      return NextResponse.json({ success: false, error: 'Лицензия отозвана или приостановлена' }, { status: 403 });
    }

    const expiresAt = new Date(lic.expires_at).getTime();
    if (!Number.isFinite(expiresAt) || Date.now() > expiresAt) {
      return NextResponse.json({ success: false, error: 'Срок действия лицензии истёк' }, { status: 403 });
    }

    const expiresDate = new Date(expiresAt);
    const now = new Date();
    const remainingDays = Math.max(1, Math.ceil((expiresDate.getTime() - now.getTime()) / (1000 * 3600 * 24)));

    const { flvJson } = createSignedLicenseFlv({
      licenseKey: lic.license_key,
      project: lic.server_name || 'FloV:MP Server',
      issuedTo: lic.username || 'Licensee',
      plan: lic.plan || 'enterprise',
      maxPlayers: lic.max_players || 5000,
      maxServers: lic.plan === 'enterprise' || lic.plan === 'lifetime' ? 10 : 3,
      days: remainingDays,
      expiresAt: expiresDate,
    });

    return new NextResponse(flvJson, {
      status: 200,
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
        'Content-Disposition': `attachment; filename="license.flv"`,
        'Cache-Control': 'no-store',
      },
    });
  } catch (err: any) {
    console.error('Download by key error:', err);
    return NextResponse.json({ success: false, error: 'Ошибка генерации license.flv' }, { status: 500 });
  }
}
