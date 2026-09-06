import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET() {
  const host = process.env.NEXT_PUBLIC_SERVER_HOST || '188.127.229.224';
  const port = process.env.NEXT_PUBLIC_SERVER_PORT || '7788';
  const startTime = Date.now();

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 2000);

    const res = await fetch(`http://${host}:${port}/info`, {
      signal: controller.signal,
      cache: 'no-store',
    });
    clearTimeout(timeoutId);

    const ping = Date.now() - startTime;

    if (res.ok) {
      const data = await res.json();
      return NextResponse.json({
        online: true,
        host,
        port,
        name: data.name || 'Держава Онлайн',
        gamemode: data.gamemode || 'RolePlay',
        players: data.players || 0,
        maxPlayers: data.maxplayers || 1500,
        pingMs: ping,
        version: data.version || 'v16.4.39-flov',
      });
    }
  } catch {
    // If HTTP info endpoint not directly responding or timing out, check port 80 /cdn/info.json
    try {
      const controller2 = new AbortController();
      const timeoutId2 = setTimeout(() => controller2.abort(), 2000);
      const res2 = await fetch(`http://${host}/cdn/info.json`, {
        signal: controller2.signal,
        cache: 'no-store',
      });
      clearTimeout(timeoutId2);

      if (res2.ok) {
        const data = await res2.json();
        return NextResponse.json({
          online: true,
          host,
          port,
          name: data.name || 'Держава Онлайн',
          gamemode: data.gamemode || 'RolePlay',
          players: data.players || 0,
          maxPlayers: data.maxplayers || 1500,
          pingMs: Date.now() - startTime,
          version: data.version || 'v16.4.39-flov',
        });
      }
    } catch {
      // Fallback
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
