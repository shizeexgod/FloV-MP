/**
 * FloV:MP Discord Bot SDK & Integration Helper
 * Allows Discord bots to interact directly with FloV:MP servers and SaaS control plane.
 */

export interface FlovBotConfig {
  baseUrl?: string;
  projectSlug: string;
  apiKey?: string;
}

export class FlovDiscordBotClient {
  private readonly baseUrl: string;
  private readonly projectSlug: string;
  private readonly apiKey?: string;

  constructor(config: FlovBotConfig) {
    this.baseUrl = (config.baseUrl || 'https://flovmp.net').replace(/\/$/, '');
    this.projectSlug = config.projectSlug;
    this.apiKey = config.apiKey;
  }

  /**
   * Fetches public status of the server for /status command
   */
  async getStatus() {
    const res = await fetch(`${this.baseUrl}/api/v1/public/${encodeURIComponent(this.projectSlug)}/status`);
    if (!res.ok) throw new Error(`Status check failed: HTTP ${res.status}`);
    return res.json();
  }

  /**
   * Fetches active players list for /players command
   */
  async getPlayers() {
    const res = await fetch(`${this.baseUrl}/api/v1/public/${encodeURIComponent(this.projectSlug)}/players`);
    if (!res.ok) throw new Error(`Players fetch failed: HTTP ${res.status}`);
    return res.json();
  }

  /**
   * Delivers privilege or executes in-game command for /givevip or store webhooks
   */
  async deliverPrivilege(characterId: number, packageId: string, amountRub: number = 0) {
    if (!this.apiKey) throw new Error('API Key is required to deliver privileges');
    const res = await fetch(`${this.baseUrl}/api/v1/public/${encodeURIComponent(this.projectSlug)}/donate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        apiKey: this.apiKey,
        characterId,
        packageId,
        amountRub,
      }),
    });
    if (!res.ok) throw new Error(`Privilege delivery failed: HTTP ${res.status}`);
    return res.json();
  }

  /**
   * Generates a Discord Rich Embed ready for Discord.js interaction reply
   */
  formatStatusEmbed(statusData: any) {
    const server = statusData.server;
    const isOnline = server?.status === 'online';

    return {
      title: `${statusData.project?.name || 'FloV:MP Server'} · Мониторинг`,
      color: isOnline ? 0x34c759 : 0xff3b30,
      fields: [
        { name: 'Статус', value: isOnline ? '🟢 Онлайн' : '🔴 Оффлайн', inline: true },
        { name: 'Игроки', value: `\`${server?.playersOnline || 0} / ${server?.maxSlots || 1500}\``, inline: true },
        { name: 'Подключение', value: `\`${server?.endpoint || '127.0.0.1:7788'}\``, inline: true },
        { name: 'Tick Rate', value: `${server?.tickRate || 60}.0 Hz`, inline: true },
        { name: 'Движок', value: 'FloV:MP v1.0.4', inline: true },
        { name: 'FastDL', value: 'CDN Активен', inline: true },
      ],
      footer: { text: 'FloV:MP Discord SDK' },
      timestamp: new Date().toISOString(),
    };
  }
}
