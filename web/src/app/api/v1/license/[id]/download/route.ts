import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { createSignedLicenseFlv } from '@/lib/license';

export async function GET(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const licenseId = Number(params.id);
    if (!licenseId || isNaN(licenseId)) {
      return NextResponse.json({ error: 'Некорректный ID лицензии' }, { status: 400 });
    }

    const rows: any = await query('SELECT * FROM portal_licenses WHERE id = ? LIMIT 1', [licenseId]);
    if (!rows || rows.length === 0) {
      return NextResponse.json({ error: 'Лицензия не найдена' }, { status: 404 });
    }

    const lic = rows[0];

    // Проверка прав: только владелец лицензии или админ платформы
    if (Number(lic.user_id) !== Number(session.userId) && session.role !== 'owner' && session.role !== 'admin') {
      return NextResponse.json({ error: 'Нет доступа к этой лицензии' }, { status: 403 });
    }

    const expiresDate = new Date(lic.expires_at);
    const now = new Date();
    const remainingDays = Math.max(1, Math.ceil((expiresDate.getTime() - now.getTime()) / (1000 * 3600 * 24)));

    const { flvJson } = createSignedLicenseFlv({
      licenseKey: lic.license_key,
      project: lic.server_name,
      issuedTo: session.username,
      plan: lic.plan || 'enterprise',
      maxPlayers: lic.max_players || 5000,
      maxServers: lic.plan === 'enterprise' ? 10 : 3,
      days: remainingDays,
    });

    return new NextResponse(flvJson, {
      status: 200,
      headers: {
        'Content-Type': 'application/json',
        'Content-Disposition': `attachment; filename="license.flv"`,
        'Cache-Control': 'no-store',
      },
    });
  } catch (err: any) {
    console.error('Download license.flv error:', err);
    return NextResponse.json({ error: 'Ошибка скачивания файла лицензии' }, { status: 500 });
  }
}
