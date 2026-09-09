import { NextRequest, NextResponse } from 'next/server';
import {
  queueAgentCommand,
  pollPendingCommands,
  completeAgentCommand,
  getServerById,
  getProjectById,
} from '@/lib/db';
import { getSessionUser } from '@/lib/auth';

// GET: агент сервера опрашивает очередь команд — авторизация по agent-токену.
export async function GET(req: NextRequest) {
  try {
    const token =
      req.nextUrl.searchParams.get('token') ||
      req.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
    if (!token) {
      return NextResponse.json({ success: false, error: 'Agent token required' }, { status: 401 });
    }

    const commands = await pollPendingCommands(token);
    return NextResponse.json({ success: true, count: commands.length, commands });
  } catch {
    return NextResponse.json({ success: false, error: 'internal error' }, { status: 500 });
  }
}

// POST: дашборд ставит команду серверу. ТОЛЬКО владелец проекта этого
// сервера либо администратор платформы. Раньше эндпоинт был без авторизации
// вообще — любой мог поставить серверу restart/stop/kick_all.
export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ success: false, error: 'Требуется вход' }, { status: 401 });
    }

    const body = await req.json().catch(() => null);
    if (!body) return NextResponse.json({ success: false, error: 'bad json' }, { status: 400 });
    const { serverId, command, payload } = body;

    if (!serverId || !command) {
      return NextResponse.json(
        { success: false, error: 'serverId and command are required' },
        { status: 400 },
      );
    }

    // 'execute' убран: произвольная команда агенту — это RCE на гейм-сервере.
    const validCommands = [
      'restart', 'stop', 'broadcast', 'kick_all',
      'resource_start', 'resource_stop', 'resource_restart',
    ];
    if (!validCommands.includes(command)) {
      return NextResponse.json(
        { success: false, error: `Invalid command. Allowed: ${validCommands.join(', ')}` },
        { status: 400 },
      );
    }

    const server = await getServerById(Number(serverId));
    if (!server) {
      return NextResponse.json({ success: false, error: 'Server not found' }, { status: 404 });
    }

    const isPlatformAdmin = ['owner', 'admin'].includes(session.role);
    if (!isPlatformAdmin) {
      const project = await getProjectById(Number((server as any).project_id));
      if (!project || Number((project as any).user_id) !== Number(session.userId)) {
        return NextResponse.json({ success: false, error: 'Нет доступа к этому серверу' }, { status: 403 });
      }
    }

    // payload для broadcast — только строка, ограниченная по длине
    let safePayload = payload;
    if (command === 'broadcast') {
      safePayload = String(payload?.message ?? payload ?? '').slice(0, 500);
    }

    const commandId = await queueAgentCommand(Number(serverId), command, safePayload);
    return NextResponse.json({
      success: true,
      message: `Command '${command}' queued for server ${server.name}`,
      commandId,
    });
  } catch {
    return NextResponse.json({ success: false, error: 'internal error' }, { status: 500 });
  }
}

// PATCH: агент сервера отчитывается о выполнении — авторизация по agent-токену.
export async function PATCH(req: NextRequest) {
  try {
    const token =
      req.nextUrl.searchParams.get('token') ||
      req.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
    if (!token) {
      return NextResponse.json({ success: false, error: 'Agent token required' }, { status: 401 });
    }

    const body = await req.json().catch(() => null);
    if (!body) return NextResponse.json({ success: false, error: 'bad json' }, { status: 400 });
    const { commandId, status = 'executed', result } = body;
    if (!commandId) {
      return NextResponse.json({ success: false, error: 'commandId is required' }, { status: 400 });
    }

    const st: 'executed' | 'failed' = status === 'failed' ? 'failed' : 'executed';
    const ok = await completeAgentCommand(Number(commandId), st, result);
    return NextResponse.json({ success: ok });
  } catch {
    return NextResponse.json({ success: false, error: 'internal error' }, { status: 500 });
  }
}
