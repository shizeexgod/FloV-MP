import { NextResponse } from 'next/server';
import { getProjectBySlug, getServersByProject } from '@/lib/db';

// Public, credential-free connection manifest for a customer's own launcher.
// The portal owns licensing; the game endpoint belongs to the customer's VDS.
export async function GET(
  _req: Request,
  { params }: { params: { slug: string } }
) {
  try {
    if (!/^[a-z0-9][a-z0-9-]{0,62}$/.test(params.slug || '')) {
      return NextResponse.json({ error: 'Invalid project slug' }, { status: 400 });
    }
    const project = await getProjectBySlug(params.slug);
    if (!project || Number(project.is_active) !== 1) {
      return NextResponse.json({ error: 'Project unavailable' }, { status: 404 });
    }

    const servers = await getServersByProject(project.id);
    const server = servers.find((item) => item.environment === 'production');
    if (!server) {
      return NextResponse.json({ error: 'Production server not configured' }, { status: 409 });
    }

    const host = String(server.ip || '').trim().toLowerCase();
    const port = Number(server.port);
    // The current Legacy client accepts IPv4/DNS names, not IPv6 literals.
    if (!/^(?=.{1,253}$)[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(host) ||
        host === 'localhost' || host === '0.0.0.0' || host.startsWith('127.') ||
        !Number.isInteger(port) || port < 1 || port > 65515) {
      return NextResponse.json({ error: 'Public server endpoint not configured' }, { status: 409 });
    }

    return NextResponse.json({
      project: { slug: project.slug, name: project.name },
      server: {
        name: server.name,
        host,
        port,
        endpoint: `${host}:${port}`,
        gatewayPort: port + 10,
        online: server.status === 'online',
      },
      launch: { protocolUrl: `flovmp://connect/${host}:${port}` },
    }, { headers: { 'Cache-Control': 'no-store' } });
  } catch {
    return NextResponse.json({ error: 'Connection manifest unavailable' }, { status: 500 });
  }
}
