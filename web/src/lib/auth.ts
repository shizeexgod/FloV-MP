import jwt from 'jsonwebtoken';
import bcrypt from 'bcryptjs';
import { cookies } from 'next/headers';

const JWT_SECRET = process.env.JWT_SECRET || 'flovmp_super_secure_jwt_secret_2026_platform_master_key';

export interface TokenPayload {
  userId: number;
  email: string;
  username: string;
  role: string;
}

export async function hashPassword(password: string): Promise<string> {
  return await bcrypt.hash(password, 10);
}

export async function comparePassword(password: string, hash: string): Promise<boolean> {
  return await bcrypt.compare(password, hash);
}

export function signToken(payload: TokenPayload): string {
  return jwt.sign(payload, JWT_SECRET, { expiresIn: '7d' });
}

export function verifyToken(token: string): TokenPayload | null {
  try {
    return jwt.verify(token, JWT_SECRET) as TokenPayload;
  } catch {
    return null;
  }
}

export function getSessionUser(): TokenPayload | null {
  try {
    const cookieStore = cookies();
    const token = cookieStore.get('flovmp_auth')?.value;
    if (!token) return null;
    return verifyToken(token);
  } catch {
    return null;
  }
}
