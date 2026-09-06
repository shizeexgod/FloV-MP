import { NextRequest, NextResponse } from 'next/server';
import { getServersByProject, createServer, getProjectById } from '@/lib/db';

export async function GET(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Project not found' }, { status: 404 });
    }

    const servers = await getServersByProject(projectId);
    return NextResponse.json({ success: true, project, servers });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const body = await req.json();
    const { environment = 'production', name, ip = '127.0.0.1', port = 7788, maxPlayers = 1500 } = body;

    if (!name) {
      return NextResponse.json({ success: false, error: 'Server name is required' }, { status: 400 });
    }

    const server = await createServer(
      projectId,
      environment as 'production' | 'development' | 'test',
      name.trim(),
      ip.trim(),
      Number(port),
      Number(maxPlayers)
    );

    return NextResponse.json({ success: true, server }, { status: 201 });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
