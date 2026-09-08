import { NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { AuthError, requireUser } from '@/lib/rbac';
import { generateTotpSecret, totpAuthUri } from '@/lib/totp';

/** GET — current 2FA status for the signed-in user. */
export async function GET() {
  try {
    const session = requireUser();
    const rows = await query<Record<string, unknown>>(
      'SELECT totp_enabled FROM portal_users WHERE id = ? LIMIT 1',
      [session.userId]
    );
    return NextResponse.json({ enabled: Boolean(rows[0]?.totp_enabled) });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ error: err.message }, { status: err.status });
    return NextResponse.json({ error: err.message }, { status: 500 });
  }
}

/** POST — begin enrolment: generate a pending secret and an otpauth:// URI. */
export async function POST() {
  try {
    const session = requireUser();
    const secret = generateTotpSecret();
    await query('UPDATE portal_users SET totp_pending = ? WHERE id = ?', [secret, session.userId]);
    return NextResponse.json({
      secret,
      otpauthUri: totpAuthUri(secret, session.email),
      hint: 'Отсканируйте QR или введите ключ в приложении-аутентификаторе, затем подтвердите кодом на /enable',
    });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ error: err.message }, { status: err.status });
    return NextResponse.json({ error: err.message }, { status: 500 });
  }
}
