import { NextRequest, NextResponse } from 'next/server';
import { getProjectBySlug, getServersByProject } from '@/lib/db';

export async function GET(
  req: NextRequest,
  { params }: { params: { slug: string } }
) {
  try {
    const slug = params.slug;
    if (!slug) {
      return NextResponse.json({ success: false, error: 'Project slug required' }, { status: 400 });
    }

    const project = await getProjectBySlug(slug);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Project not found' }, { status: 404 });
    }

    const servers = await getServersByProject(project.id);
    const prodServer = servers.find((s) => s.environment === 'production') || servers[0];

    const totalOnline = servers.reduce((acc, s) => acc + (s.status === 'online' ? s.players_count : 0), 0);

    return NextResponse.json({
      success: true,
      project: {
        name: project.name,
        slug: project.slug,
        plan: project.plan,
        maxPlayers: project.max_players,
        totalOnline,
      },
      server: prodServer
        ? {
            name: prodServer.name,
            endpoint: `${prodServer.ip}:${prodServer.port}`,
            status: prodServer.status,
            playersOnline: prodServer.players_count,
            maxSlots: prodServer.max_players,
            tickRate: prodServer.tick_rate,
            engineVersion: '1.0.4',
            lastHeartbeat: prodServer.last_heartbeat,
          }
        : null,
      meta: {
        engine: 'FloV:MP Server Runtime',
        protocol: 'UDP 7788',
        fastDl: true,
        timestamp: new Date().toISOString(),
      },
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
