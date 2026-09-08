import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { comparePassword, signToken } from '@/lib/auth';
import { BadJsonError, badRequest, readJson } from '@/lib/http';
import { verifyTotp } from '@/lib/totp';

export async function POST(req: NextRequest) {
  try {
    let body: { email?: string; password?: string; totpToken?: string };
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
    const { email, password, totpToken } = body;

    if (!email || !password) {
      return NextResponse.json({ error: 'Укажите email и пароль' }, { status: 400 });
    }

    const users = await query('SELECT * FROM portal_users WHERE email = ? LIMIT 1', [
      String(email).toLowerCase().trim(),
    ]);
    if (users.length === 0) {
      return NextResponse.json({ error: 'Неверный email или пароль' }, { status: 401 });
    }

    const user = users[0];
    const passwordMatch = await comparePassword(password, user.password_hash);
    if (!passwordMatch) {
      return NextResponse.json({ error: 'Неверный email или пароль' }, { status: 401 });
    }

    // Second factor — RFC 6238 TOTP
    if (user.totp_enabled) {
      if (!totpToken) {
        // Password OK, but a 2FA code is still required. No session cookie yet.
        return NextResponse.json({ twoFactorRequired: true });
      }
      if (!verifyTotp(String(user.totp_secret || ''), String(totpToken))) {
        return NextResponse.json({ error: 'Неверный код двухфакторной аутентификации', twoFactorRequired: true }, { status: 401 });
      }
    }

    const token = signToken({
      userId: user.id,
      email: user.email,
      username: user.username,
      role: user.role,
    });

    const response = NextResponse.json({
      success: true,
      user: {
        id: user.id,
        email: user.email,
        username: user.username,
        role: user.role,
      },
    });

    response.cookies.set('flovmp_auth', token, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'lax',
      maxAge: 7 * 24 * 3600,
      path: '/',
    });

    return response;
  } catch (err: any) {
    console.error('Login API error:', err);
    return NextResponse.json({ error: 'Ошибка авторизации на сервере' }, { status: 500 });
  }
}
