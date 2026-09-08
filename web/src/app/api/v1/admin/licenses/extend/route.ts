import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function POST(req: NextRequest) {
  const session = getSessionUser();
  if (!session || !['owner','admin'].includes(session.role)) {
    return NextResponse.json({ error: 'Доступ разрешен только администраторам' }, { status: 403 });
  }

  let body: { licenseId?: number; days?: number };
  try {
    body = await readJson(req);
  } catch (e) {
    if (e instanceof BadJsonError) return badRequest();
    throw e;
  }
  const { licenseId, days } = body;
  if (!licenseId || !days) {
    return NextResponse.json({ error: 'Укажите ID лицензии и количество дней' }, { status: 400 });
  }

  const lics = await query('SELECT id, expires_at FROM portal_licenses WHERE id = ? LIMIT 1', [Number(licenseId)]);
  if (lics.length === 0) {
    return NextResponse.json({ error: 'Лицензия не найдена' }, { status: 404 });
  }

  const currentExp = new Date(lics[0].expires_at).getTime();
  const baseTime = currentExp > Date.now() ? currentExp : Date.now();
  const newExpiry = new Date(baseTime + Number(days) * 24 * 3600 * 1000)
    .toISOString()
    .slice(0, 19)
    .replace('T', ' ');

  await query('UPDATE portal_licenses SET expires_at = ?, is_active = 1 WHERE id = ?', [newExpiry, Number(licenseId)]);

  return NextResponse.json({
    success: true,
    newExpiry,
    message: `Лицензия продлена на ${days} дней (до ${newExpiry})`,
  });
}
