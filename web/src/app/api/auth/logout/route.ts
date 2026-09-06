import { NextResponse } from 'next/server';

export async function POST() {
  const response = NextResponse.json({ success: true, message: 'Сессия завершена' });
  response.cookies.set('flovmp_auth', '', {
    httpOnly: true,
    expires: new Date(0),
    path: '/',
  });
  return response;
}
