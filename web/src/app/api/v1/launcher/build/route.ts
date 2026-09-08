import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    let parsed: any;
    try {
      parsed = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
    const { licenseId, projectName, primaryColor, serverIp, serverPort, logoUrl } = parsed;

    if (!projectName || !licenseId) {
      return NextResponse.json({ error: 'Укажите название проекта и ID лицензии' }, { status: 400 });
    }

    // Verify license ownership
    const lics = await query('SELECT id, license_key FROM portal_licenses WHERE id = ? AND user_id = ? LIMIT 1', [
      Number(licenseId),
      session.userId,
    ]);

    if (lics.length === 0) {
      return NextResponse.json({ error: 'Лицензия не найдена' }, { status: 404 });
    }

    const safeName = projectName.trim().replace(/[^a-zA-Zа-яА-Я0-9 _-]/g, '');
    const cleanColor = /^#[0-9A-Fa-f]{6}$/.test(primaryColor) ? primaryColor : '#ff3d8a';
    const downloadUrl = `/cdn/${encodeURIComponent(safeName)}-Launcher-Setup.exe`;

    const res: any = await query(
      'INSERT INTO portal_launcher_builds (license_id, project_name, primary_color, logo_url, build_status, download_url) VALUES (?, ?, ?, ?, ?, ?)',
      [Number(licenseId), safeName, cleanColor, logoUrl || null, 'ready', downloadUrl]
    );

    return NextResponse.json({
      success: true,
      buildId: res.insertId,
      projectName: safeName,
      primaryColor: cleanColor,
      status: 'ready',
      downloadUrl,
      config: {
        serverIp: serverIp || '188.127.229.224',
        serverPort: Number(serverPort || 7788),
        licenseKey: lics[0].license_key,
      },
      message: 'Кастомный лаунчер успешно собран и готов к загрузке!',
    });
  } catch (err: any) {
    console.error('Launcher build error:', err);
    return NextResponse.json({ error: 'Ошибка компиляции лаунчера' }, { status: 500 });
  }
}
