import { NextRequest, NextResponse } from 'next/server';
import { updateProjectSettings, getProjectById } from '@/lib/db';
import { AuthError, assertProjectAccess, requireUser } from '@/lib/rbac';
import { BadJsonError, badRequest, readJson } from '@/lib/http';

export async function PATCH(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const session = requireUser();
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    assertProjectAccess(session, project);

    let body: any;
    try {
      body = await readJson(req);
    } catch (e) {
      if (e instanceof BadJsonError) return badRequest();
      throw e;
    }
    const {
      hwidPolicy,
      allowVpn,
      maxAccountsPerHwid,
      discordWebhookUrl,
      telegramWebhookToken,
      telegramChatId,
      webhookAlertsEnabled,
    } = body;

    const ok = await updateProjectSettings(projectId, {
      hwidPolicy,
      allowVpn,
      maxAccountsPerHwid: maxAccountsPerHwid !== undefined ? Number(maxAccountsPerHwid) : undefined,
      discordWebhookUrl,
      telegramWebhookToken,
      telegramChatId,
      webhookAlertsEnabled,
    });

    return NextResponse.json({
      success: ok,
      message: 'Настройки проекта успешно обновлены',
    });
  } catch (err: any) {
    if (err instanceof AuthError) return NextResponse.json({ success: false, error: err.message }, { status: err.status });
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
