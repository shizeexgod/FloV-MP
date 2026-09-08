import { NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET() {
  const session = getSessionUser();
  if (!session) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  const users = await query<Record<string, unknown>>(
    'SELECT id, email, username, role, telegram, discord, created_at FROM portal_users WHERE id = ?',
    [session.userId]
  );
  if (users.length === 0) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  const u = users[0];
  // Whitelist fields — the JSON fallback store ignores the SELECT column list,
  // so never trust it to have stripped password_hash.
  return NextResponse.json({
    authenticated: true,
    user: {
      id: u.id,
      email: u.email,
      username: u.username,
      role: u.role,
      telegram: u.telegram ?? null,
      discord: u.discord ?? null,
      created_at: u.created_at ?? null,
    },
  });
}
