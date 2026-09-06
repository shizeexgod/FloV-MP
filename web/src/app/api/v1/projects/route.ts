import { NextRequest, NextResponse } from 'next/server';
import { getProjectsByUser, createProject } from '@/lib/db';
import { cookies } from 'next/headers';
import jwt from 'jsonwebtoken';

const JWT_SECRET = process.env.JWT_SECRET || 'flovmp_super_secret_jwt_key_2026';

function getUserIdFromRequest(): number {
  try {
    const token = cookies().get('portal_token')?.value;
    if (token) {
      const decoded: any = jwt.verify(token, JWT_SECRET);
      if (decoded && decoded.id) return Number(decoded.id);
    }
  } catch {}
  return 1; // Default to primary project owner if not signed in or dev mode
}

export async function GET() {
  try {
    const userId = getUserIdFromRequest();
    const projects = await getProjectsByUser(userId);
    return NextResponse.json({ success: true, projects });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(req: NextRequest) {
  try {
    const userId = getUserIdFromRequest();
    const body = await req.json();
    const { name, slug, plan = 'enterprise', maxPlayers = 1500 } = body;

    if (!name || !slug) {
      return NextResponse.json({ success: false, error: 'Name and slug are required' }, { status: 400 });
    }

    const cleanSlug = slug
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9-]/g, '-');

    const project = await createProject(userId, name.trim(), cleanSlug, plan, Number(maxPlayers));
    return NextResponse.json({ success: true, project }, { status: 201 });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
