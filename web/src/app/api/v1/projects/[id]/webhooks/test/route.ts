import { NextRequest, NextResponse } from 'next/server';
import { getProjectById, getServersByProject } from '@/lib/db';
import { dispatchDiscordAlert, dispatchTelegramAlert } from '@/lib/webhookDispatcher';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';

export async function POST(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const projectMaybe = await getProjectById(projectId);
    assertProjectAccess(session, projectMaybe);
    const project = projectMaybe!;

    const servers = await getServersByProject(projectId);
    const mainServer = servers[0] || {
      name: 'Production Node',
      ip: '188.127.229.224',
      port: 7788,
      players_count: 142,
      tick_rate: 60.0,
      memory_mb: 284,
    };

    const body = await req.json().catch(() => ({}));
    const targetType = body.type || 'all'; // 'discord' | 'telegram' | 'all'

    const testPayload = {
      event: 'SERVER_RECOVERED' as const,
      projectName: project.name,
      serverName: mainServer.name,
      serverIp: `${mainServer.ip}:${mainServer.port}`,
      details: {
        incidentId: `TEST-${Date.now().toString(36).toUpperCase()}`,
        reason: 'Тестовый алерт проверки интеграции Webhook FloV:MP',
        tickRate: mainServer.tick_rate,
        memoryMb: mainServer.memory_mb,
        playersOnline: mainServer.players_count,
      },
    };

    const results: { discord?: boolean; telegram?: boolean } = {};

    if ((targetType === 'discord' || targetType === 'all') && project.discord_webhook_url) {
      results.discord = await dispatchDiscordAlert(project.discord_webhook_url, testPayload);
    }

    if (
      (targetType === 'telegram' || targetType === 'all') &&
      project.telegram_webhook_token &&
      project.telegram_chat_id
    ) {
      results.telegram = await dispatchTelegramAlert(
        project.telegram_webhook_token,
        project.telegram_chat_id,
        testPayload
      );
    }

    return NextResponse.json({
      success: true,
      message: 'Тестовые алерты отправлены',
      results,
    });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
