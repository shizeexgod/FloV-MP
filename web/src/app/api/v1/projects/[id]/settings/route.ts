import { NextRequest, NextResponse } from 'next/server';
import { updateProjectSettings, getProjectById } from '@/lib/db';

export async function PATCH(req: NextRequest, { params }: { params: { id: string } }) {
  try {
    const projectId = Number(params.id);
    if (isNaN(projectId)) {
      return NextResponse.json({ success: false, error: 'Invalid project ID' }, { status: 400 });
    }

    const project = await getProjectById(projectId);
    if (!project) {
      return NextResponse.json({ success: false, error: 'Project not found' }, { status: 404 });
    }

    const body = await req.json();
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
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
