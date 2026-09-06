import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const { licenseId, serverIp, serverName } = await req.json();

    if (!licenseId || !serverIp) {
      return NextResponse.json({ error: 'Укажите ID лицензии и новый IP-адрес' }, { status: 400 });
    }

    const ipClean = serverIp.trim();
    // Validate IPv4 format or 0.0.0.0
    const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
    if (!ipRegex.test(ipClean)) {
      return NextResponse.json({ error: 'Некорректный формат IPv4 адреса (например: 188.127.229.224)' }, { status: 400 });
    }

    // Check ownership
    const lics = await query('SELECT id FROM portal_licenses WHERE id = ? AND user_id = ? LIMIT 1', [
      licenseId,
      session.userId,
    ]);

    if (lics.length === 0) {
      return NextResponse.json({ error: 'Лицензия не найдена или принадлежит другому пользователю' }, { status: 404 });
    }

    if (serverName) {
      await query('UPDATE portal_licenses SET bound_ip = ?, server_name = ? WHERE id = ? AND user_id = ?', [
        ipClean,
        serverName.trim(),
        licenseId,
        session.userId,
      ]);
    } else {
      await query('UPDATE portal_licenses SET bound_ip = ? WHERE id = ? AND user_id = ?', [
        ipClean,
        licenseId,
        session.userId,
      ]);
    }

    return NextResponse.json({
      success: true,
      boundIp: ipClean,
      message: `IP успешно привязан к лицензии (${ipClean})`,
    });
  } catch (err: any) {
    console.error('Bind IP error:', err);
    return NextResponse.json({ error: 'Ошибка обновления IP-адреса' }, { status: 500 });
  }
}
