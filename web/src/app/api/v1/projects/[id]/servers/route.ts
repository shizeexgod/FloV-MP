import { NextRequest, NextResponse } from 'next/server';
import { getServersByProject, createServer, getProjectById } from '@/lib/db';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function GET(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    assertProjectAccess(session, project);

    const servers = await getServersByProject(projectId);
    return NextResponse.json({ success: true, project, servers });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    assertProjectAccess(session, project);

    let body: any;
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
    const { environment = 'production', name, ip = '127.0.0.1', port = 7788, maxPlayers = 1500 } = body;

    if (!name) {
      return NextResponse.json({ success: false, error: 'Server name is required' }, { status: 400 });
    }

    const server = await createServer(
      projectId,
      environment as 'production' | 'development' | 'test',
      String(name).trim(),
      String(ip).trim(),
      Number(port),
      Number(maxPlayers)
    );

    return NextResponse.json({ success: true, server }, { status: 201 });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
