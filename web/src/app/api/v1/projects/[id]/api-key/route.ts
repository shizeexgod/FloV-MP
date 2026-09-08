import { NextRequest, NextResponse } from 'next/server';
import { getProjectById, rotateProjectApiKey } from '@/lib/db';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';

/** POST — rotate the project Agent API key. Owner or platform admin only. */
export async function POST(_req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }
    assertProjectAccess(session, await getProjectById(projectId));

    const apiKey = await rotateProjectApiKey(projectId);
    return NextResponse.json({ success: true, apiKey, message: 'Agent API key rotated' });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
