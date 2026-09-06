import { NextRequest, NextResponse } from 'next/server';
import { getResourcesByServer, queueAgentCommand, setResourceStatus, getServerById } from '@/lib/db';

export async function GET(
  req: NextRequest,
  { params }: { params: { id: string; serverId: string } }
) {
  try {
    const serverId = Number(params.serverId);
    if (isNaN(serverId)) {
      return NextResponse.json({ success: false, error: 'Invalid server ID' }, { status: 400 });
    }

    const resources = await getResourcesByServer(serverId);
    return NextResponse.json({ success: true, resources });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(
  req: NextRequest,
  { params }: { params: { id: string; serverId: string } }
) {
  try {
    const serverId = Number(params.serverId);
    if (isNaN(serverId)) {
      return NextResponse.json({ success: false, error: 'Invalid server ID' }, { status: 400 });
    }

    const server = await getServerById(serverId);
    if (!server) {
      return NextResponse.json({ success: false, error: 'Server not found' }, { status: 404 });
    }

    const body = await req.json();
    const { action, resourceName } = body;

    if (!action || !resourceName) {
      return NextResponse.json(
        { success: false, error: 'Action and resourceName are required' },
        { status: 400 }
      );
    }

    const validActions = ['start', 'stop', 'restart'];
    if (!validActions.includes(action)) {
      return NextResponse.json(
        { success: false, error: `Invalid action. Must be one of: ${validActions.join(', ')}` },
        { status: 400 }
      );
    }

    const commandType = `resource_${action}` as 'resource_start' | 'resource_stop' | 'resource_restart';
    const commandId = await queueAgentCommand(serverId, commandType, resourceName);

    // Update local resource state optimistic
    const targetStatus = action === 'stop' ? 'stopped' : 'running';
    await setResourceStatus(serverId, resourceName, targetStatus);

    return NextResponse.json({
      success: true,
      message: `Команда ${commandType} для ресурса '${resourceName}' поставлена в очередь агенту`,
      commandId,
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
