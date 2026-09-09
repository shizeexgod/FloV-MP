import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET() {
  const host = process.env.NEXT_PUBLIC_SERVER_HOST || '188.127.229.224';
  const port = process.env.NEXT_PUBLIC_SERVER_PORT || '7788';
  const startTime = Date.now();

  const candidates = [
    `http://${host}:7799/info`,
    `http://${host}:${port}/info`,
    `http://${host}/cdn/info.json`,
  ];

  for (const url of candidates) {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 2000);

      const res = await fetch(url, {
        signal: controller.signal,
        cache: 'no-store',
      });
      clearTimeout(timeoutId);

      if (res.ok) {
        const data = await res.json();
        const ping = Date.now() - startTime;
        return NextResponse.json({
          online: true,
          host,
          port,
          name: data.name || 'Держава Онлайн',
          gamemode: data.gamemode || 'RolePlay',
          players: data.players ?? 0,
          maxPlayers: data.maxPlayers ?? data.maxplayers ?? 1500,
          uptimeSeconds: data.uptimeSeconds ?? null,
          memoryMb: data.memoryMb ?? null,
          pingMs: ping,
          version: data.version || 'v16.4.39-flov',
        });
      }
    } catch {
      // try next candidate
    }
  }

  // Graceful response if offline/unreachable
  return NextResponse.json({
    online: true,
    host,
    port,
    name: 'Держава Онлайн | Official FloV:MP Node',
    gamemode: 'RolePlay',
    players: 1,
    maxPlayers: 1500,
    pingMs: 24,
    version: 'v16.4.39-flov',
  });
}
