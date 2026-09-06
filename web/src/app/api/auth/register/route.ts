import { NextRequest, NextResponse } from 'next/server';
import { query } from '@/lib/db';
import { hashPassword, signToken } from '@/lib/auth';
import { generateLicenseKey } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    const { username, email, password, telegram, discord } = await req.json();

    if (!email || !password || !username) {
      return NextResponse.json({ error: 'Заполните обязательные поля (логин, email, пароль)' }, { status: 400 });
    }

    if (password.length < 6) {
      return NextResponse.json({ error: 'Пароль должен содержать не менее 6 символов' }, { status: 400 });
    }

    // Check if user exists
    const existing = await query('SELECT id FROM portal_users WHERE email = ? LIMIT 1', [email.toLowerCase().trim()]);
    if (existing.length > 0) {
      return NextResponse.json({ error: 'Пользователь с таким email уже зарегистрирован' }, { status: 409 });
    }

    const hashed = await hashPassword(password);
    const userRes: any = await query(
      'INSERT INTO portal_users (email, username, password_hash, telegram, discord) VALUES (?, ?, ?, ?, ?)',
      [email.toLowerCase().trim(), username.trim(), hashed, telegram?.trim() || null, discord?.trim() || null]
    );

    const userId = userRes.insertId || 1;

    // Issue initial starter license (Indie tier, 30 days, 128 slots)
    const initialKey = generateLicenseKey('FLV');
    const expiresAt = new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString().slice(0, 19).replace('T', ' ');
    
    await query(
      'INSERT INTO portal_licenses (user_id, license_key, server_name, bound_ip, plan, max_players, expires_at) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [userId, initialKey, `${username.trim()} RP Server`, '0.0.0.0', 'indie', 128, expiresAt]
    );

    const token = signToken({
      userId,
      email: email.toLowerCase().trim(),
      username: username.trim(),
      role: 'client',
    });

    const response = NextResponse.json({
      success: true,
      user: { id: userId, email, username, role: 'client' },
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
    console.error('Register API error:', err);
    return NextResponse.json({ error: 'Ошибка сервера при регистрации' }, { status: 500 });
  }
}
