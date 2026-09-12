import { NextRequest, NextResponse } from 'next/server';
import { getServerById, getProjectById, updateServerSlotLimit } from '@/lib/db';
import { AuthError, requireUser, assertProjectAccess } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

/**
 * PUT /api/v1/servers/[id]/slots — управление лимитом слотов сервера.
 * Body: { slotLimit: number | null }  (null = без ограничения)
 */
export async function PUT(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const serverId = Number(params.id);
    if (!serverId || isNaN(serverId)) return badRequest('Некорректный ID сервера');

    const server = await getServerById(serverId);
    if (!server) return NextResponse.json({ success: false, error: 'Сервер не найден' }, { status: 404 });

    const project = await getProjectById(server.project_id);
    assertProjectAccess(session, project);

    let body: any;
    try { body = await readJson(req); }
    catch (e) { if (e instanceof BadJsonError) return badRequest(); throw e; }

    const slotLimit = body.slotLimit === null || body.slotLimit === undefined
      ? null
      : Number(body.slotLimit);

    if (slotLimit !== null && (isNaN(slotLimit) || slotLimit < 1 || slotLimit > 10000)) {
      return badRequest('slotLimit должен быть от 1 до 10000 или null (без ограничения)');
    }

    const ok = await updateServerSlotLimit(serverId, slotLimit);
    return NextResponse.json({
      success: ok,
      serverId,
      slotLimit,
      message: slotLimit === null ? 'Лимит снят — без ограничения' : `Лимит установлен: ${slotLimit} слотов`,
    });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
