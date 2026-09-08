import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function POST(req: NextRequest) {
  const session = getSessionUser();
  if (!session || !['owner','admin'].includes(session.role)) {
    return NextResponse.json({ error: 'Доступ разрешен только администраторам' }, { status: 403 });
  }

  let body: { licenseId?: number; isActive?: boolean };
  try {
    body = await readJson(req);
  } catch (e) {
    if (e instanceof BadJsonError) return badRequest();
    throw e;
  }
  const { licenseId, isActive } = body;
  if (!licenseId) {
    return NextResponse.json({ error: 'Укажите ID лицензии' }, { status: 400 });
  }

  await query('UPDATE portal_licenses SET is_active = ? WHERE id = ?', [isActive ? 1 : 0, Number(licenseId)]);

  return NextResponse.json({
    success: true,
    message: `Лицензия #${licenseId} ${isActive ? 'активирована' : 'приостановлена'}`,
  });
}
