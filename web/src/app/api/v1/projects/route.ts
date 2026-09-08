import { NextRequest, NextResponse } from 'next/server';
import { getProjectsByUser, createProject } from '@/lib/db';
import { AuthError, isPlatformAdmin, requireUser } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function GET() {
  try {
    const session = requireUser();
    // Platform admins see every project; clients see their own.
    const projects = await getProjectsByUser(isPlatformAdmin(session) ? 0 : session.userId, isPlatformAdmin(session));
    return NextResponse.json({ success: true, projects });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(req: NextRequest) {
  try {
    const session = requireUser();

    let body: any;
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
    const { name, slug, plan = 'enterprise', maxPlayers = 1500 } = body;

    if (!name || !slug) {
      return NextResponse.json({ success: false, error: 'Name and slug are required' }, { status: 400 });
    }

    const cleanSlug = String(slug)
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9-]/g, '-');

    const project = await createProject(session.userId, String(name).trim(), cleanSlug, plan, Number(maxPlayers));
    return NextResponse.json({ success: true, project }, { status: 201 });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
