import { NextRequest, NextResponse } from 'next/server';
import { getServerById, getProjectById, updateServerMeta } from '@/lib/db';
import { AuthError, requireUser, assertProjectAccess } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

const VALID_ENVIRONMENTS = ['production', 'development', 'test', 'staging'] as const;

/**
 * PUT /api/v1/servers/[id]/meta — управление метаданными сервера.
 * Body: { label?: string, environment?: string, notes?: string, name?: string }
 *
 * label — визуальный префикс в ЛК (напр. "Основной", "Тест PvP")
 * environment — тип сервера: production, development, test, staging
 * notes — заметки владельца
 * name — имя сервера
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

    // Валидация
    const update: Record<string, any> = {};

    if (body.label !== undefined) {
      update.label = body.label === null ? null : String(body.label).trim().substring(0, 64);
    }
    if (body.environment !== undefined) {
      if (!VALID_ENVIRONMENTS.includes(body.environment)) {
        return badRequest(`environment должен быть: ${VALID_ENVIRONMENTS.join(', ')}`);
      }
      update.environment = body.environment;
    }
    if (body.notes !== undefined) {
      update.notes = body.notes === null ? null : String(body.notes).trim().substring(0, 1000);
    }
    if (body.name !== undefined) {
      const name = String(body.name).trim();
      if (name.length < 1 || name.length > 128) return badRequest('Имя сервера: 1-128 символов');
      update.name = name;
    }

    if (Object.keys(update).length === 0) return badRequest('Не указаны поля для обновления');

    const ok = await updateServerMeta(serverId, update);
    return NextResponse.json({ success: ok, serverId, updated: update });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
