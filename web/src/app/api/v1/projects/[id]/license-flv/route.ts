import { NextRequest, NextResponse } from 'next/server';
import { getProjectById } from '@/lib/db';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';
import { createSignedLicenseFlv } from '@/lib/license';

/**
 * GET /api/v1/projects/[id]/license-flv
 * Скачивание готового подписанного файла `license.flv` для игрового сервера FloV:MP.
 */
export async function GET(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (!projectId || isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Некорректный ID проекта' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Проект не найден' }, { status: 404 });
    }
    assertProjectAccess(session, project);

    const expiresDate = new Date(project.expires_at || Date.now() + 365 * 24 * 3600 * 1000);
    const now = new Date();
    const remainingDays = Math.max(1, Math.ceil((expiresDate.getTime() - now.getTime()) / (1000 * 3600 * 24)));

    const { flvJson } = createSignedLicenseFlv({
      licenseKey: project.license_key,
      project: project.name,
      issuedTo: session.username,
      plan: project.plan || 'enterprise',
      maxPlayers: project.max_players || 5000,
      maxServers: 10,
      days: remainingDays,
    });

    return new NextResponse(flvJson, {
      status: 200,
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
        'Content-Disposition': `attachment; filename="license.flv"`,
        'Cache-Control': 'no-store',
      },
    });
  } catch (err: any) {
    if (err instanceof AuthError) {
      return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    }
    console.error('Download project license.flv error:', err);
    return NextResponse.json({ success: false, error: 'Ошибка скачивания файла лицензии' }, { status: 500 });
  }
}
