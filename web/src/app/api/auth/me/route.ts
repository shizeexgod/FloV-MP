import { NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET() {
  const session = getSessionUser();
  if (!session) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  const users = await query('SELECT id, email, username, role, telegram, discord, created_at FROM portal_users WHERE id = ?', [session.userId]);
  if (users.length === 0) {
    return NextResponse.json({ authenticated: false }, { status: 401 });
  }

  return NextResponse.json({
    authenticated: true,
    user: users[0],
  });
}
