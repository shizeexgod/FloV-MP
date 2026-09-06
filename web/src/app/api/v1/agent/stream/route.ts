import { NextRequest } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET(req: NextRequest) {
  const encoder = new TextEncoder();
  const stream = new TransformStream();
  const writer = stream.writable.getWriter();

  // Send initial connected event
  const sendEvent = async (data: object) => {
    try {
      await writer.write(encoder.encode(`data: ${JSON.stringify(data)}\n\n`));
    } catch {}
  };

  void (async () => {
    await sendEvent({
      type: 'connected',
      time: new Date().toLocaleTimeString('ru-RU'),
      tag: 'ControlPlane',
      text: 'WebSocket/SSE Real-Time Log Stream established with RemoteServerAgent',
      tone: 'info',
    });

    let counter = 0;
    const interval = setInterval(async () => {
      counter++;
      const timeStr = new Date().toLocaleTimeString('ru-RU');
      const sampleEvents = [
        {
          type: 'log',
          time: timeStr,
          tag: 'Sync',
          text: `EntityStreamer: 128 entities synchronized across 8 spatial grid cells (${Math.floor(Math.random() * 5) + 1} ms)`,
          tone: 'info',
        },
        {
          type: 'log',
          time: timeStr,
          tag: 'Telemetry',
          text: `Performance tick: 60.0 Hz tickrate, 60.0 FPS, ${Math.floor(Math.random() * 15) + 245} MB CoreCLR`,
          tone: 'info',
        },
        {
          type: 'log',
          time: timeStr,
          tag: 'Shield',
          text: 'FloV:Shield invariant verification passed (0 anomalies detected)',
          tone: 'info',
        },
      ];

      const evt = sampleEvents[counter % sampleEvents.length];
      try {
        await sendEvent(evt);
      } catch {
        clearInterval(interval);
      }

      if (counter > 100) {
        clearInterval(interval);
        try {
          await writer.close();
        } catch {}
      }
    }, 4000);

    req.signal.addEventListener('abort', () => {
      clearInterval(interval);
      try {
        writer.close();
      } catch {}
    });
  })();

  return new Response(stream.readable, {
    headers: {
      'Content-Type': 'text/event-stream; charset=utf-8',
      'Cache-Control': 'no-cache, no-transform',
      Connection: 'keep-alive',
    },
  });
}
