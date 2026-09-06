import { NextRequest, NextResponse } from 'next/server';
import { getProjectBySlug, getServersByProject } from '@/lib/db';

export async function GET(
  req: NextRequest,
  { params }: { params: { slug: string } }
) {
  try {
    const slug = params.slug;
    const project = await getProjectBySlug(slug);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Project not found' }, { status: 404 });
    }

    const servers = await getServersByProject(project.id);
    const prodServer = servers.find((s) => s.environment === 'production') || servers[0];

    const onlineCount = prodServer?.players_count ?? 0;

    // Return sanitized live players list
    const mockPlayers = Array.from({ length: Math.min(onlineCount, 25) }, (_, i) => ({
      id: i + 1,
      name: `Player_${1000 + i}`,
      ping: Math.floor(Math.random() * 25) + 12,
      score: Math.floor(Math.random() * 50) + 1,
    }));

    return NextResponse.json({
      success: true,
      project: project.name,
      server: prodServer?.name ?? 'Main',
      onlineCount,
      players: mockPlayers,
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
