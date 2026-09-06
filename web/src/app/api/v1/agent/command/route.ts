import { NextRequest, NextResponse } from 'next/server';
import {
  queueAgentCommand,
  pollPendingCommands,
  completeAgentCommand,
  getServerById,
} from '@/lib/db';

// GET: Server Agent polls pending commands
export async function GET(req: NextRequest) {
  try {
    const token = req.nextUrl.searchParams.get('token') || req.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
    if (!token) {
      return NextResponse.json({ success: false, error: 'Agent token required' }, { status: 401 });
    }

    const commands = await pollPendingCommands(token);
    return NextResponse.json({ success: true, count: commands.length, commands });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

// POST: Dashboard queues a command for a server
export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { serverId, command, payload } = body;

    if (!serverId || !command) {
      return NextResponse.json({ success: false, error: 'serverId and command are required' }, { status: 400 });
    }

    const validCommands = ['restart', 'stop', 'broadcast', 'kick_all', 'execute', 'resource_start', 'resource_stop', 'resource_restart'];
    if (!validCommands.includes(command)) {
      return NextResponse.json(
        { success: false, error: `Invalid command. Allowed: ${validCommands.join(', ')}` },
        { status: 400 }
      );
    }

    const server = await getServerById(Number(serverId));
    if (!server) {
      return NextResponse.json({ success: false, error: 'Server not found' }, { status: 404 });
    }

    const commandId = await queueAgentCommand(Number(serverId), command, payload);
    return NextResponse.json({
      success: true,
      message: `Command '${command}' queued successfully for server ${server.name}`,
      commandId,
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

// PATCH: Server agent reports command execution result
export async function PATCH(req: NextRequest) {
  try {
    const body = await req.json();
    const { commandId, status = 'executed', result } = body;

    if (!commandId) {
      return NextResponse.json({ success: false, error: 'commandId is required' }, { status: 400 });
    }

    const success = await completeAgentCommand(Number(commandId), status, result);
    return NextResponse.json({ success });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
