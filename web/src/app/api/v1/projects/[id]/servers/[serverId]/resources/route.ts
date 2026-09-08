import { NextRequest, NextResponse } from 'next/server';
import { getResourcesByServer, queueAgentCommand, setResourceStatus, getServerById, getProjectById } from '@/lib/db';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function GET(
  req: NextRequest,
  { params }: { params: { id: string; serverId: string } }
) {
  try {
    const session = requireUser();
    const serverId = Number(params.serverId);
    if (isNaN(serverId)) {
      return NextResponse.json({ success: false, error: 'Invalid server ID' }, { status: 400 });
    }
    assertProjectAccess(session, await getProjectById(Number(params.id)));

    const resources = await getResourcesByServer(serverId);
    return NextResponse.json({ success: true, resources });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}

export async function POST(
  req: NextRequest,
  { params }: { params: { id: string; serverId: string } }
) {
  try {
    const session = requireUser();
    const serverId = Number(params.serverId);
    if (isNaN(serverId)) {
      return NextResponse.json({ success: false, error: 'Invalid server ID' }, { status: 400 });
    }

    assertProjectAccess(session, await getProjectById(Number(params.id)));
    const server = await getServerById(serverId);
    if (!server) {
      return NextResponse.json({ success: false, error: 'Server not found' }, { status: 404 });
    }

    let body: any;
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
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
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
