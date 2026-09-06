/**
 * FloV:MP Webhook Dispatcher
 * Dispatches automated alerts to Discord channels and Telegram chats
 * upon server crashes, high CPU/RAM usage, low tickrate, and security events.
 */

export interface WebhookAlertPayload {
  event: 'SERVER_CRASH' | 'HIGH_LOAD' | 'SERVER_RECOVERED' | 'SECURITY_HWID_FLAG';
  projectName: string;
  serverName: string;
  serverIp: string;
  details: {
    reason?: string;
    incidentId?: string;
    tickRate?: number;
    memoryMb?: number;
    cpuPercent?: number;
    playersOnline?: number;
    hwidHash?: string;
    playerFlovId?: string;
  };
}

export async function dispatchDiscordAlert(
  webhookUrl: string,
  alert: WebhookAlertPayload
): Promise<boolean> {
  if (!webhookUrl || !webhookUrl.startsWith('https://discord.com/api/webhooks/')) {
    return false;
  }

  const colorMap = {
    SERVER_CRASH: 0xff3b30, // Red
    HIGH_LOAD: 0xff9500, // Amber
    SERVER_RECOVERED: 0x34c759, // Green
    SECURITY_HWID_FLAG: 0xaf52de, // Violet
  };

  const titleMap = {
    SERVER_CRASH: '🚨 Сбой сервера / Аварийный перезапуск',
    HIGH_LOAD: '⚠️ Предупреждение о высокой нагрузке на узел',
    SERVER_RECOVERED: '✅ Сервер успешно восстановлен и онлайн',
    SECURITY_HWID_FLAG: '🛡️ Срабатывание политики безопасности FloV:ID',
  };

  const fields: Array<{ name: string; value: string; inline?: boolean }> = [
    { name: 'Проект', value: alert.projectName, inline: true },
    { name: 'Сервер', value: `${alert.serverName} (${alert.serverIp})`, inline: true },
  ];

  if (alert.details.incidentId) {
    fields.push({ name: 'ID инцидента', value: `\`${alert.details.incidentId}\``, inline: true });
  }

  if (alert.details.reason) {
    fields.push({ name: 'Причина', value: alert.details.reason, inline: false });
  }

  if (alert.details.tickRate !== undefined) {
    fields.push({ name: 'Tick Rate', value: `${alert.details.tickRate} Hz`, inline: true });
  }

  if (alert.details.memoryMb !== undefined) {
    fields.push({ name: 'ОЗУ CoreCLR', value: `${alert.details.memoryMb} MB`, inline: true });
  }

  if (alert.details.playersOnline !== undefined) {
    fields.push({ name: 'Игроки', value: `${alert.details.playersOnline}`, inline: true });
  }

  if (alert.details.hwidHash) {
    fields.push({ name: 'HWID Hash', value: `\`${alert.details.hwidHash.slice(0, 16)}...\``, inline: true });
  }

  const payload = {
    username: 'FloV:MP Watchdog',
    avatar_url: 'https://flovmp.net/branding/logo-codex.png',
    embeds: [
      {
        title: titleMap[alert.event] || 'Уведомление FloV:MP',
        color: colorMap[alert.event] || 0xff3d8a,
        fields,
        footer: { text: 'FloV:MP Cloud Control Plane · 2026' },
        timestamp: new Date().toISOString(),
      },
    ],
  };

  try {
    const res = await fetch(webhookUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    return res.ok;
  } catch {
    return false;
  }
}

export async function dispatchTelegramAlert(
  botToken: string,
  chatId: string,
  alert: WebhookAlertPayload
): Promise<boolean> {
  if (!botToken || !chatId) return false;

  let message = `*FloV:MP Watchdog Alert*\n`;
  message += `━━━━━━━━━━━━━━━━━━━━\n`;
  message += `📍 *Проект:* ${alert.projectName}\n`;
  message += `🖥️ *Сервер:* ${alert.serverName} (\`${alert.serverIp}\`)\n`;

  if (alert.details.incidentId) {
    message += `🆔 *Инцидент:* \`${alert.details.incidentId}\`\n`;
  }
  if (alert.details.reason) {
    message += `⚠️ *Причина:* ${alert.details.reason}\n`;
  }
  if (alert.details.tickRate !== undefined) {
    message += `⚡ *Tick Rate:* ${alert.details.tickRate} Hz\n`;
  }
  if (alert.details.memoryMb !== undefined) {
    message += `💾 *ОЗУ:* ${alert.details.memoryMb} MB\n`;
  }
  message += `━━━━━━━━━━━━━━━━━━━━\n`;
  message += `_FloV:MP Cloud Control Plane · ${new Date().toLocaleTimeString('ru-RU')}_`;

  const url = `https://api.telegram.org/bot${botToken}/sendMessage`;

  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        chat_id: chatId,
        text: message,
        parse_mode: 'Markdown',
      }),
    });
    return res.ok;
  } catch {
    return false;
  }
}
