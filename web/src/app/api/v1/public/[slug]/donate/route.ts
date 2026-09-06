import { NextRequest, NextResponse } from 'next/server';
import { getProjectBySlug, getServersByProject, queueAgentCommand } from '@/lib/db';

export async function POST(
  req: NextRequest,
  { params }: { params: { slug: string } }
) {
  try {
    const slug = params.slug;
    const project = await getProjectBySlug(slug);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Project not found' }, { status: 404 });
    }

    const body = await req.json();
    const { apiKey, characterId, packageId, amountRub, customCommand } = body;

    // Verify API Key
    if (apiKey !== project.api_key) {
      return NextResponse.json({ success: false, error: 'Unauthorized: Invalid Project API Key' }, { status: 401 });
    }

    if (!characterId && !customCommand) {
      return NextResponse.json(
        { success: false, error: 'characterId or customCommand is required' },
        { status: 400 }
      );
    }

    const servers = await getServersByProject(project.id);
    const prodServer = servers.find((s) => s.environment === 'production') || servers[0];

    if (!prodServer) {
      return NextResponse.json({ success: false, error: 'No active production server' }, { status: 503 });
    }

    // Prepare command to execute in server agent
    const commandPayload = customCommand || `givevip ${characterId} ${packageId || 'vip_gold'} ${amountRub || 0}`;
    const commandId = await queueAgentCommand(prodServer.id, 'execute', commandPayload);

    return NextResponse.json({
      success: true,
      message: 'Донат успешно принят и передан серверному агенту',
      transactionId: `DON-${Date.now()}`,
      commandId,
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
