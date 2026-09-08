import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { AuthError, requireUser } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';
import { verifyTotp } from '@/lib/totp';

export async function POST(req: NextRequest) {
  try {
    const session = requireUser();

    let body: { token?: string };
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }

    const rows = await query<Record<string, unknown>>(
      'SELECT totp_secret, totp_enabled FROM portal_users WHERE id = ? LIMIT 1',
      [session.userId]
    );
    if (!rows[0]?.totp_enabled) {
      return NextResponse.json({ error: '2FA не была включена' }, { status: 409 });
    }
    if (!verifyTotp(String(rows[0].totp_secret || ''), String(body.token || ''))) {
      return NextResponse.json({ error: 'Неверный код' }, { status: 400 });
    }

    await query('UPDATE portal_users SET totp_secret = ?, totp_pending = ?, totp_enabled = ? WHERE id = ?', [
      null,
      null,
      0,
      session.userId,
    ]);

    return NextResponse.json({ success: true, message: 'Двухфакторная аутентификация отключена' });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ error: err.message }, { status: err.status });
    return NextResponse.json({ error: err.message }, { status: 500 });
  }
}
