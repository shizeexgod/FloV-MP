import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function POST(req: NextRequest) {
  const session = getSessionUser();
  if (!session || session.role !== 'admin') {
    return NextResponse.json({ error: 'Доступ разрешен только администраторам' }, { status: 403 });
  }

  const { licenseId, isActive } = await req.json();
  if (!licenseId) {
    return NextResponse.json({ error: 'Укажите ID лицензии' }, { status: 400 });
  }

  await query('UPDATE portal_licenses SET is_active = ? WHERE id = ?', [isActive ? 1 : 0, Number(licenseId)]);

  return NextResponse.json({
    success: true,
    message: `Лицензия #${licenseId} ${isActive ? 'активирована' : 'приостановлена'}`,
  });
}
