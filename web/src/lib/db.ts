import mysql, { Pool } from 'mysql2/promise';
import fs from 'fs';
import path from 'path';

export interface InvoiceRecord {
  id: number;
  user_id: number;
  license_id?: number;
  amount_rub: number;
  plan: string;
  payment_method: string;
  payment_id?: string;
  status: 'pending' | 'paid' | 'cancelled';
  created_at: string;
  paid_at?: string;
}

export interface LauncherBuildRecord {
  id: number;
  license_id: number;
  project_name: string;
  primary_color: string;
  logo_url?: string;
  build_status: 'queued' | 'building' | 'ready';
  download_url?: string;
  created_at: string;
}

export interface TelemetryRecord {
  id: number;
  license_key: string;
  players: number;
  max_players: number;
  tick_rate: number;
  memory_mb: number;
  fps: number;
  server_ip: string;
  recorded_at: string;
}

export interface ProjectRecord {
  id: number;
  user_id: number;
  name: string;
  slug: string;
  license_key: string;
  plan: string;
  max_players: number;
  api_key: string;
  hwid_policy?: 'strict' | 'lenient' | 'disabled';
  allow_vpn?: boolean;
  max_accounts_per_hwid?: number;
  discord_webhook_url?: string;
  telegram_webhook_token?: string;
  telegram_chat_id?: string;
  webhook_alerts_enabled?: boolean;
  is_active: number;
  expires_at: string;
  created_at: string;
}

export interface ServerRecord {
  id: number;
  project_id: number;
  environment: 'production' | 'development' | 'test';
  name: string;
  ip: string;
  port: number;
  agent_token: string;
  status: 'online' | 'offline' | 'restarting';
  players_count: number;
  max_players: number;
  tick_rate: number;
  memory_mb: number;
  cpu_percent: number;
  last_heartbeat?: string;
  created_at: string;
}

export interface ResourceRecord {
  id: number;
  server_id: number;
  name: string;
  type: 'gamemode' | 'script' | 'map' | 'vehicles' | 'ui';
  status: 'running' | 'stopped' | 'failed';
  version: string;
  author: string;
  started_at?: string;
}

export interface AgentCommandRecord {
  id: number;
  server_id: number;
  command:
    | 'restart'
    | 'stop'
    | 'broadcast'
    | 'kick_all'
    | 'execute'
    | 'resource_start'
    | 'resource_stop'
    | 'resource_restart';
  payload?: string;
  status: 'pending' | 'executed' | 'failed';
  result?: string;
  dispatched_at: string;
  executed_at?: string;
}

export interface MockStore {
  users: Array<{
    id: number;
    email: string;
    username: string;
    password_hash: string;
    role: string;
    telegram?: string;
    discord?: string;
    created_at: string;
  }>;
  licenses: Array<{
    id: number;
    user_id: number;
    license_key: string;
    server_name: string;
    bound_ip: string;
    plan: string;
    max_players: number;
    is_active: number;
    expires_at: string;
    created_at: string;
    last_verified_at?: string;
  }>;
  invoices: InvoiceRecord[];
  launcher_builds: LauncherBuildRecord[];
  telemetry: TelemetryRecord[];
  projects?: ProjectRecord[];
  servers?: ServerRecord[];
  agent_commands?: AgentCommandRecord[];
}

// On Vercel the bundle dir is read-only — only /tmp is writable. The JSON store
// is a local-dev / MySQL-down fallback only; a real deploy must point DB_* at a
// managed MariaDB. /tmp is ephemeral per lambda, which is acceptable for that.
const DB_DIR = process.env.VERCEL ? '/tmp/flovmp' : path.join(process.cwd(), 'data');
const DB_FILE = path.join(DB_DIR, 'portal-db.json');

function getStore(): MockStore {
  try {
    if (!fs.existsSync(DB_DIR)) {
      fs.mkdirSync(DB_DIR, { recursive: true });
    }
    if (fs.existsSync(DB_FILE)) {
      const data = fs.readFileSync(DB_FILE, 'utf-8');
      const parsed = JSON.parse(data);
      if (!parsed.invoices) parsed.invoices = [];
      if (!parsed.launcher_builds) parsed.launcher_builds = [];
      if (!parsed.telemetry) parsed.telemetry = [];
      if (!parsed.projects) parsed.projects = [];
      if (!parsed.servers) parsed.servers = [];
      if (!parsed.agent_commands) parsed.agent_commands = [];
      if (parsed.projects.length === 0) {
        parsed.projects = [
          {
            id: 1,
            user_id: 1,
            name: 'Держава Онлайн',
            slug: 'derzhava-rp',
            license_key: 'FLV-ENTERPRISE-2026-DERZHAVA',
            plan: 'enterprise',
            max_players: 1500,
            api_key: 'flv_live_derzhava_7a8f19c2',
            is_active: 1,
            expires_at: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
            created_at: new Date().toISOString(),
          },
        ];
        parsed.servers = [
          {
            id: 1,
            project_id: 1,
            environment: 'production',
            name: 'Main Production Node #1',
            ip: '188.127.229.224',
            port: 7788,
            agent_token: 'agnt_live_prod_99f48a',
            status: 'online',
            players_count: 142,
            max_players: 1500,
            tick_rate: 60.0,
            memory_mb: 384,
            cpu_percent: 18.5,
            last_heartbeat: new Date().toISOString(),
            created_at: new Date().toISOString(),
          },
          {
            id: 2,
            project_id: 1,
            environment: 'development',
            name: 'Core Dev Local',
            ip: '127.0.0.1',
            port: 7788,
            agent_token: 'agnt_dev_local_11bc23',
            status: 'online',
            players_count: 2,
            max_players: 64,
            tick_rate: 60.0,
            memory_mb: 198,
            cpu_percent: 4.2,
            last_heartbeat: new Date().toISOString(),
            created_at: new Date().toISOString(),
          },
          {
            id: 3,
            project_id: 1,
            environment: 'test',
            name: 'QA Staging Sandbox',
            ip: '127.0.0.1',
            port: 7789,
            agent_token: 'agnt_test_sandbox_55ee78',
            status: 'offline',
            players_count: 0,
            max_players: 32,
            tick_rate: 0.0,
            memory_mb: 0,
            cpu_percent: 0.0,
            created_at: new Date().toISOString(),
          },
        ];
        saveStore(parsed);
      }
      return parsed;
    }
  } catch (err) {
    console.error('Error reading mock DB file:', err);
  }

  const initialStore: MockStore = {
    users: [
      {
        id: 1,
        email: 'owner@flovmp.ru',
        username: 'Owner',
        password_hash: '$2a$10$tJd4qW5gH2Hw80rKqI22v.E43O4B1O.y4bCqgU9tKkLgE16j1Kz2S',
        role: 'admin',
        telegram: '@flovmp_dev',
        discord: 'FloVMP#0001',
        created_at: new Date().toISOString(),
      },
    ],
    licenses: [
      {
        id: 1,
        user_id: 1,
        license_key: 'FLV-ENTERPRISE-2026-DERZHAVA',
        server_name: 'Держава Онлайн | Тестовый Сервер #1',
        bound_ip: '188.127.229.224',
        plan: 'enterprise',
        max_players: 1500,
        is_active: 1,
        expires_at: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
        created_at: new Date().toISOString(),
        last_verified_at: new Date().toISOString(),
      },
    ],
    invoices: [
      {
        id: 1,
        user_id: 1,
        license_id: 1,
        amount_rub: 49000,
        plan: 'enterprise',
        payment_method: 'card',
        payment_id: 'pay_init_derzhava_2026',
        status: 'paid',
        created_at: new Date().toISOString(),
        paid_at: new Date().toISOString(),
      },
    ],
    launcher_builds: [
      {
        id: 1,
        license_id: 1,
        project_name: 'Держава Онлайн',
        primary_color: '#ff3d8a',
        build_status: 'ready',
        download_url: '/cdn/Derzhava-Launcher-Setup.exe',
        created_at: new Date().toISOString(),
      },
    ],
    telemetry: [
      {
        id: 1,
        license_key: 'FLV-ENTERPRISE-2026-DERZHAVA',
        players: 1,
        max_players: 1500,
        tick_rate: 60,
        memory_mb: 248,
        fps: 60,
        server_ip: '188.127.229.224',
        recorded_at: new Date().toISOString(),
      },
    ],
    projects: [
      {
        id: 1,
        user_id: 1,
        name: 'Держава Онлайн',
        slug: 'derzhava-rp',
        license_key: 'FLV-ENTERPRISE-2026-DERZHAVA',
        plan: 'enterprise',
        max_players: 1500,
        api_key: 'flv_live_derzhava_7a8f19c2',
        is_active: 1,
        expires_at: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
        created_at: new Date().toISOString(),
      },
    ],
    servers: [
      {
        id: 1,
        project_id: 1,
        environment: 'production',
        name: 'Main Production Node #1',
        ip: '188.127.229.224',
        port: 7788,
        agent_token: 'agnt_live_prod_99f48a',
        status: 'online',
        players_count: 142,
        max_players: 1500,
        tick_rate: 60.0,
        memory_mb: 384,
        cpu_percent: 18.5,
        last_heartbeat: new Date().toISOString(),
        created_at: new Date().toISOString(),
      },
      {
        id: 2,
        project_id: 1,
        environment: 'development',
        name: 'Core Dev Local',
        ip: '127.0.0.1',
        port: 7788,
        agent_token: 'agnt_dev_local_11bc23',
        status: 'online',
        players_count: 2,
        max_players: 64,
        tick_rate: 60.0,
        memory_mb: 198,
        cpu_percent: 4.2,
        last_heartbeat: new Date().toISOString(),
        created_at: new Date().toISOString(),
      },
      {
        id: 3,
        project_id: 1,
        environment: 'test',
        name: 'QA Staging Sandbox',
        ip: '127.0.0.1',
        port: 7789,
        agent_token: 'agnt_test_sandbox_55ee78',
        status: 'offline',
        players_count: 0,
        max_players: 32,
        tick_rate: 0.0,
        memory_mb: 0,
        cpu_percent: 0.0,
        created_at: new Date().toISOString(),
      },
    ],
    agent_commands: [],
  };

  saveStore(initialStore);
  return initialStore;
}

function saveStore(store: MockStore) {
  try {
    if (!fs.existsSync(DB_DIR)) {
      fs.mkdirSync(DB_DIR, { recursive: true });
    }
    fs.writeFileSync(DB_FILE, JSON.stringify(store, null, 2), 'utf-8');
  } catch (err) {
    console.error('Error writing mock DB file:', err);
  }
}

const globalForDb = globalThis as unknown as {
  pool?: Pool;
  mysqlUnavailable?: boolean;
};

function getPool(): Pool {
  if (!globalForDb.pool) {
    globalForDb.pool = mysql.createPool({
      host: process.env.DB_HOST || '127.0.0.1',
      port: Number(process.env.DB_PORT) || 3306,
      user: process.env.DB_USER || 'flovmp',
      password: process.env.DB_PASSWORD || 'DerzhavaFloVMP2026!Secure',
      database: process.env.DB_NAME || 'derzhava_rp',
      waitForConnections: true,
      connectionLimit: 10,
      queueLimit: 0,
      connectTimeout: 1500,
    });
  }
  return globalForDb.pool;
}

export async function query<T = any>(sql: string, params: any[] = []): Promise<T[]> {
  if (globalForDb.mysqlUnavailable) {
    return mockQueryFallback(sql, params) as unknown as T[];
  }

  try {
    const p = getPool();
    const [rows] = await p.execute(sql, params);
    return rows as T[];
  } catch (err: any) {
    globalForDb.mysqlUnavailable = true;
    console.warn(`[DB NOTICE] MySQL unreachable (${err.message}). Using shared persistent store.`);
    return mockQueryFallback(sql, params) as unknown as T[];
  }
}

function mockQueryFallback(sql: string, params: any[]): any {
  const store = getStore();
  const s = sql.toLowerCase();

  // 1. Find user by email
  if (s.includes('from portal_users') && s.includes('email =')) {
    const email = params[0];
    const user = store.users.find((u) => u.email.toLowerCase() === String(email).toLowerCase());
    return user ? [user] : [];
  }

  // 2. Find user by id
  if (s.includes('from portal_users') && /\bwhere\s+id\s*=/i.test(s)) {
    const id = Number(params[0]);
    const user = store.users.find((u) => u.id === id);
    return user ? [user] : [];
  }

  // 3. All users (for admin)
  if (s.includes('from portal_users') && !s.includes('where')) {
    return store.users;
  }

  // 4. Insert user
  if (s.includes('insert into portal_users')) {
    const [email, username, password_hash, telegram, discord] = params;
    const newId = store.users.length + 1;
    const newUser = {
      id: newId,
      email: String(email),
      username: String(username),
      password_hash: String(password_hash),
      role: 'client',
      telegram: telegram ? String(telegram) : undefined,
      discord: discord ? String(discord) : undefined,
      created_at: new Date().toISOString(),
    };
    store.users.push(newUser);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  // 4b. Update user 2FA columns (totp_secret / totp_pending / totp_enabled)
  if (s.includes('update portal_users set')) {
    const idParam = params[params.length - 1];
    const user = store.users.find((u) => u.id === Number(idParam));
    if (!user) return { affectedRows: 0 };
    const u = user as Record<string, unknown>;
    let pi = 0;
    if (s.includes('totp_secret')) u.totp_secret = params[pi++] == null ? null : String(params[pi - 1]);
    if (s.includes('totp_pending')) u.totp_pending = params[pi++] == null ? null : String(params[pi - 1]);
    if (s.includes('totp_enabled')) u.totp_enabled = Number(params[pi++]) ? 1 : 0;
    saveStore(store);
    return { affectedRows: 1 };
  }

  // 5. Find license by id (and optional user_id)
  if (s.includes('from portal_licenses') && /\bwhere\s+id\s*=/i.test(s)) {
    if (s.includes('user_id')) {
      const [id, userId] = params;
      const lic = store.licenses.find((l) => l.id === Number(id) && l.user_id === Number(userId));
      return lic ? [lic] : [];
    }
    const id = Number(params[0]);
    const lic = store.licenses.find((l) => l.id === id);
    return lic ? [lic] : [];
  }

  // 6. Find licenses by user_id
  if (s.includes('from portal_licenses') && /user_id\s*=/i.test(s)) {
    const userId = Number(params[0]);
    return store.licenses.filter((l) => l.user_id === userId);
  }

  // 7. Find license by key
  if (s.includes('from portal_licenses') && s.includes('license_key =')) {
    const key = String(params[0]);
    return store.licenses.filter((l) => l.license_key === key);
  }

  // 8. All licenses (for admin)
  if (s.includes('from portal_licenses') && !s.includes('where')) {
    return store.licenses;
  }

  // 9. Update license bound_ip
  if (s.includes('update portal_licenses set bound_ip =')) {
    if (s.includes('server_name =')) {
      const [bound_ip, server_name, id, user_id] = params;
      const lic = store.licenses.find((l) => l.id === Number(id) && (user_id ? l.user_id === Number(user_id) : true));
      if (lic) {
        lic.bound_ip = String(bound_ip);
        lic.server_name = String(server_name);
        saveStore(store);
        return { affectedRows: 1 };
      }
    } else {
      const [bound_ip, id, user_id] = params;
      const lic = store.licenses.find((l) => l.id === Number(id) && (user_id ? l.user_id === Number(user_id) : true));
      if (lic) {
        lic.bound_ip = String(bound_ip);
        saveStore(store);
        return { affectedRows: 1 };
      }
    }
    return { affectedRows: 0 };
  }

  // 10. Update last_verified_at
  if (s.includes('update portal_licenses set last_verified_at')) {
    const [id] = params;
    const lic = store.licenses.find((l) => l.id === Number(id));
    if (lic) {
      lic.last_verified_at = new Date().toISOString();
      saveStore(store);
      return { affectedRows: 1 };
    }
    return { affectedRows: 0 };
  }

  // 11. Update license status or plan or expiration (for admin / billing)
  if (s.includes('update portal_licenses set')) {
    if (s.includes('is_active =')) {
      const [isActive, id] = params;
      const lic = store.licenses.find((l) => l.id === Number(id));
      if (lic) {
        lic.is_active = Number(isActive);
        saveStore(store);
        return { affectedRows: 1 };
      }
    }
    if (s.includes('expires_at =')) {
      const [expiresAt, id] = params;
      const lic = store.licenses.find((l) => l.id === Number(id));
      if (lic) {
        lic.expires_at = String(expiresAt);
        saveStore(store);
        return { affectedRows: 1 };
      }
    }
  }

  // 12. Create new license
  if (s.includes('insert into portal_licenses')) {
    const [user_id, license_key, server_name, bound_ip, plan, max_players, expires_at] = params;
    const newId = store.licenses.length + 1;
    const newLic = {
      id: newId,
      user_id: Number(user_id),
      license_key: String(license_key),
      server_name: String(server_name),
      bound_ip: String(bound_ip),
      plan: String(plan),
      max_players: Number(max_players),
      is_active: 1,
      expires_at: String(expires_at),
      created_at: new Date().toISOString(),
    };
    store.licenses.push(newLic);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  // 13. Invoices queries
  if (s.includes('from portal_invoices') && /user_id\s*=/i.test(s)) {
    const userId = Number(params[0]);
    return store.invoices.filter((i) => i.user_id === userId);
  }

  if (s.includes('from portal_invoices') && /\bwhere\s+id\s*=/i.test(s)) {
    const id = Number(params[0]);
    return store.invoices.filter((i) => i.id === id);
  }

  if (s.includes('from portal_invoices') && !s.includes('where')) {
    return store.invoices;
  }

  if (s.includes('insert into portal_invoices')) {
    const [user_id, license_id, amount_rub, plan, payment_method, payment_id, status] = params;
    const newId = store.invoices.length + 1;
    const newInv: InvoiceRecord = {
      id: newId,
      user_id: Number(user_id),
      license_id: license_id ? Number(license_id) : undefined,
      amount_rub: Number(amount_rub),
      plan: String(plan),
      payment_method: String(payment_method || 'card'),
      payment_id: payment_id ? String(payment_id) : `inv_${Date.now()}`,
      status: status || 'pending',
      created_at: new Date().toISOString(),
    };
    store.invoices.push(newInv);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  if (s.includes('update portal_invoices set status =')) {
    const [status, id] = params;
    const inv = store.invoices.find((i) => i.id === Number(id));
    if (inv) {
      inv.status = status;
      if (status === 'paid') inv.paid_at = new Date().toISOString();
      saveStore(store);
      return { affectedRows: 1 };
    }
  }

  // 14. Launcher builds queries
  if (s.includes('from portal_launcher_builds') && /license_id\s*=/i.test(s)) {
    const licId = Number(params[0]);
    return store.launcher_builds.filter((b) => b.license_id === licId);
  }

  if (s.includes('insert into portal_launcher_builds')) {
    const [license_id, project_name, primary_color, logo_url, build_status, download_url] = params;
    const newId = store.launcher_builds.length + 1;
    const newBuild: LauncherBuildRecord = {
      id: newId,
      license_id: Number(license_id),
      project_name: String(project_name),
      primary_color: String(primary_color || '#ff3d8a'),
      logo_url: logo_url ? String(logo_url) : undefined,
      build_status: build_status || 'ready',
      download_url: download_url ? String(download_url) : `/cdn/${encodeURIComponent(project_name)}-Launcher.exe`,
      created_at: new Date().toISOString(),
    };
    store.launcher_builds.push(newBuild);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  // 15. Telemetry queries
  if (s.includes('insert into portal_telemetry')) {
    const [license_key, players, max_players, tick_rate, memory_mb, fps, server_ip] = params;
    const newId = store.telemetry.length + 1;
    const rec: TelemetryRecord = {
      id: newId,
      license_key: String(license_key),
      players: Number(players || 0),
      max_players: Number(max_players || 1500),
      tick_rate: Number(tick_rate || 60),
      memory_mb: Number(memory_mb || 0),
      fps: Number(fps || 60),
      server_ip: String(server_ip || '127.0.0.1'),
      recorded_at: new Date().toISOString(),
    };
    store.telemetry.push(rec);
    if (store.telemetry.length > 500) {
      store.telemetry.shift();
    }
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  if (s.includes('from portal_telemetry') && s.includes('license_key =')) {
    const key = String(params[0]);
    return store.telemetry.filter((t) => t.license_key === key).slice(-30);
  }

  // 16. Projects queries
  if (s.includes('from portal_projects')) {
    if (/user_id\s*=/i.test(s)) {
      const uid = Number(params[0]);
      return (store.projects || []).filter((p) => p.user_id === uid);
    }
    if (/slug\s*=/i.test(s)) {
      const slug = String(params[0]);
      return (store.projects || []).filter((p) => p.slug === slug);
    }
    if (/\bid\s*=/i.test(s)) {
      const id = Number(params[0]);
      return (store.projects || []).filter((p) => p.id === id);
    }
    if (/api_key\s*=/i.test(s)) {
      const key = String(params[0]);
      return (store.projects || []).filter((p) => p.api_key === key);
    }
    return store.projects || [];
  }

  if (s.includes('insert into portal_projects')) {
    const [user_id, name, slug, license_key, plan, max_players, api_key, expires_at] = params;
    if (!store.projects) store.projects = [];
    const newId = store.projects.length + 1;
    const newProj: ProjectRecord = {
      id: newId,
      user_id: Number(user_id),
      name: String(name),
      slug: String(slug),
      license_key: String(license_key),
      plan: String(plan || 'enterprise'),
      max_players: Number(max_players || 1500),
      api_key: String(api_key),
      is_active: 1,
      expires_at: String(expires_at),
      created_at: new Date().toISOString(),
    };
    store.projects.push(newProj);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  // 16b. Dynamic UPDATE portal_projects SET col = ?, ... WHERE id = ?
  if (s.includes('update portal_projects set')) {
    const cols = (sql.match(/set\s+(.+?)\s+where/i)?.[1] || '')
      .split(',')
      .map((c) => c.trim().split('=')[0].trim());
    const id = Number(params[params.length - 1]);
    const proj = (store.projects || []).find((p) => p.id === id);
    if (!proj) return { affectedRows: 0 };
    const pr = proj as unknown as Record<string, unknown>;
    cols.forEach((col, i) => {
      const v = params[i];
      pr[col] = v === null || v === undefined ? null : typeof v === 'number' ? v : String(v);
    });
    saveStore(store);
    return { affectedRows: 1 };
  }

  // 17. Servers queries
  if (s.includes('from portal_servers')) {
    if (/project_id\s*=/i.test(s)) {
      const pid = Number(params[0]);
      return (store.servers || []).filter((srv) => srv.project_id === pid);
    }
    if (/agent_token\s*=/i.test(s)) {
      const token = String(params[0]);
      return (store.servers || []).filter((srv) => srv.agent_token === token);
    }
    if (/\bid\s*=/i.test(s)) {
      const id = Number(params[0]);
      return (store.servers || []).filter((srv) => srv.id === id);
    }
    return store.servers || [];
  }

  if (s.includes('insert into portal_servers')) {
    const [project_id, environment, name, ip, port, agent_token, max_players] = params;
    if (!store.servers) store.servers = [];
    const newId = store.servers.length + 1;
    const newSrv: ServerRecord = {
      id: newId,
      project_id: Number(project_id),
      environment: environment || 'production',
      name: String(name),
      ip: String(ip || '127.0.0.1'),
      port: Number(port || 7788),
      agent_token: String(agent_token),
      status: 'offline',
      players_count: 0,
      max_players: Number(max_players || 1500),
      tick_rate: 60.0,
      memory_mb: 0,
      cpu_percent: 0.0,
      created_at: new Date().toISOString(),
    };
    store.servers.push(newSrv);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  if (s.includes('update portal_servers set')) {
    if (s.includes('players_count =') && s.includes('agent_token =')) {
      const [players, max_players, tick_rate, memory_mb, cpu_percent, status, last_heartbeat, token] = params;
      const srv = (store.servers || []).find((x) => x.agent_token === String(token));
      if (srv) {
        srv.players_count = Number(players);
        srv.max_players = Number(max_players);
        srv.tick_rate = Number(tick_rate);
        srv.memory_mb = Number(memory_mb);
        srv.cpu_percent = Number(cpu_percent);
        srv.status = status as 'online' | 'offline';
        srv.last_heartbeat = String(last_heartbeat);
        saveStore(store);
        return { affectedRows: 1 };
      }
    }
    if (s.includes('status =') && /\bid\s*=/i.test(s)) {
      const [status, id] = params;
      const srv = (store.servers || []).find((x) => x.id === Number(id));
      if (srv) {
        srv.status = status as 'online' | 'offline' | 'restarting';
        saveStore(store);
        return { affectedRows: 1 };
      }
    }
  }

  // 18. Agent commands queries
  if (s.includes('from portal_agent_commands')) {
    if (/server_id\s*=/i.test(s) && /status\s*=/i.test(s)) {
      const [sid, status] = params;
      return (store.agent_commands || []).filter((c) => c.server_id === Number(sid) && c.status === String(status));
    }
    return store.agent_commands || [];
  }

  if (s.includes('insert into portal_agent_commands')) {
    const [server_id, command, payload, status] = params;
    if (!store.agent_commands) store.agent_commands = [];
    const newId = store.agent_commands.length + 1;
    const newCmd: AgentCommandRecord = {
      id: newId,
      server_id: Number(server_id),
      command: command,
      payload: payload ? String(payload) : undefined,
      status: (status as 'pending') || 'pending',
      dispatched_at: new Date().toISOString(),
    };
    store.agent_commands.push(newCmd);
    saveStore(store);
    return { insertId: newId, affectedRows: 1 };
  }

  if (s.includes('update portal_agent_commands set status =')) {
    const [status, result, executed_at, id] = params;
    const cmd = (store.agent_commands || []).find((c) => c.id === Number(id));
    if (cmd) {
      cmd.status = status as 'executed' | 'failed';
      cmd.result = result ? String(result) : undefined;
      cmd.executed_at = String(executed_at);
      saveStore(store);
      return { affectedRows: 1 };
    }
  }

  return [];
}

// ---------------------------------------------------------------------------
// Typed Public Helpers for Project & Server Management
// ---------------------------------------------------------------------------

export async function getProjectsByUser(userId: number, all = false): Promise<ProjectRecord[]> {
  const rows = all
    ? await query('SELECT * FROM portal_projects ORDER BY id DESC')
    : await query('SELECT * FROM portal_projects WHERE user_id = ? ORDER BY id DESC', [userId]);
  return rows as ProjectRecord[];
}

export async function getProjectBySlug(slug: string): Promise<ProjectRecord | null> {
  const rows = await query('SELECT * FROM portal_projects WHERE slug = ? LIMIT 1', [slug]);
  const arr = rows as ProjectRecord[];
  return arr.length > 0 ? arr[0] : null;
}

export async function getProjectById(id: number): Promise<ProjectRecord | null> {
  const rows = await query('SELECT * FROM portal_projects WHERE id = ? LIMIT 1', [id]);
  const arr = rows as ProjectRecord[];
  return arr.length > 0 ? arr[0] : null;
}

export async function createProject(
  userId: number,
  name: string,
  slug: string,
  plan: string = 'enterprise',
  maxPlayers: number = 1500
): Promise<ProjectRecord> {
  const licenseKey = `FLV-PROJ-${Math.random().toString(36).substring(2, 6).toUpperCase()}-${Math.random().toString(36).substring(2, 6).toUpperCase()}`;
  const apiKey = `flv_live_${Math.random().toString(36).substring(2, 10)}_${Date.now().toString(36)}`;
  const expiresAt = new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString();

  const res: any = await query(
    'INSERT INTO portal_projects (user_id, name, slug, license_key, plan, max_players, api_key, expires_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)',
    [userId, name, slug, licenseKey, plan, maxPlayers, apiKey, expiresAt]
  );

  return {
    id: res.insertId || Date.now(),
    user_id: userId,
    name,
    slug,
    license_key: licenseKey,
    plan,
    max_players: maxPlayers,
    api_key: apiKey,
    is_active: 1,
    expires_at: expiresAt,
    created_at: new Date().toISOString(),
  };
}

export async function getServersByProject(projectId: number): Promise<ServerRecord[]> {
  const rows = await query('SELECT * FROM portal_servers WHERE project_id = ? ORDER BY id ASC', [projectId]);
  return rows as ServerRecord[];
}

export async function getServerById(id: number): Promise<ServerRecord | null> {
  const rows = await query('SELECT * FROM portal_servers WHERE id = ? LIMIT 1', [id]);
  const arr = rows as ServerRecord[];
  return arr.length > 0 ? arr[0] : null;
}

export async function getServerByToken(agentToken: string): Promise<ServerRecord | null> {
  const rows = await query('SELECT * FROM portal_servers WHERE agent_token = ? LIMIT 1', [agentToken]);
  const arr = rows as ServerRecord[];
  return arr.length > 0 ? arr[0] : null;
}

export async function createServer(
  projectId: number,
  environment: 'production' | 'development' | 'test',
  name: string,
  ip: string = '127.0.0.1',
  port: number = 7788,
  maxPlayers: number = 1500
): Promise<ServerRecord> {
  const token = `agnt_${environment}_${Math.random().toString(36).substring(2, 8)}`;
  const res: any = await query(
    'INSERT INTO portal_servers (project_id, environment, name, ip, port, agent_token, max_players) VALUES (?, ?, ?, ?, ?, ?, ?)',
    [projectId, environment, name, ip, port, token, maxPlayers]
  );

  return {
    id: res.insertId || Date.now(),
    project_id: projectId,
    environment,
    name,
    ip,
    port,
    agent_token: token,
    status: 'offline',
    players_count: 0,
    max_players: maxPlayers,
    tick_rate: 60.0,
    memory_mb: 0,
    cpu_percent: 0.0,
    created_at: new Date().toISOString(),
  };
}

export async function updateServerTelemetry(
  agentToken: string,
  data: {
    players: number;
    maxPlayers: number;
    tickRate: number;
    memoryMb: number;
    cpuPercent: number;
  }
): Promise<boolean> {
  const now = new Date().toISOString();
  const res: any = await query(
    'UPDATE portal_servers SET players_count = ?, max_players = ?, tick_rate = ?, memory_mb = ?, cpu_percent = ?, status = ?, last_heartbeat = ? WHERE agent_token = ?',
    [data.players, data.maxPlayers, data.tickRate, data.memoryMb, data.cpuPercent, 'online', now, agentToken]
  );
  return res.affectedRows > 0;
}

export async function queueAgentCommand(
  serverId: number,
  command:
    | 'restart'
    | 'stop'
    | 'broadcast'
    | 'kick_all'
    | 'execute'
    | 'resource_start'
    | 'resource_stop'
    | 'resource_restart',
  payload?: string
): Promise<number> {
  const res: any = await query(
    'INSERT INTO portal_agent_commands (server_id, command, payload, status) VALUES (?, ?, ?, ?)',
    [serverId, command, payload || null, 'pending']
  );
  return res.insertId;
}

export async function pollPendingCommands(agentToken: string): Promise<AgentCommandRecord[]> {
  const srv = await getServerByToken(agentToken);
  if (!srv) return [];
  const rows = await query(
    'SELECT * FROM portal_agent_commands WHERE server_id = ? AND status = ? ORDER BY id ASC',
    [srv.id, 'pending']
  );
  return rows as AgentCommandRecord[];
}

export async function completeAgentCommand(
  commandId: number,
  status: 'executed' | 'failed',
  result?: string
): Promise<boolean> {
  const now = new Date().toISOString();
  const res: any = await query(
    'UPDATE portal_agent_commands SET status = ?, result = ?, executed_at = ? WHERE id = ?',
    [status, result || null, now, commandId]
  );
  return res.affectedRows > 0;
}

export async function updateProjectSettings(
  projectId: number,
  settings: {
    hwidPolicy?: 'strict' | 'lenient' | 'disabled';
    allowVpn?: boolean;
    maxAccountsPerHwid?: number;
    discordWebhookUrl?: string;
    telegramWebhookToken?: string;
    telegramChatId?: string;
    webhookAlertsEnabled?: boolean;
  }
): Promise<boolean> {
  const fields: string[] = [];
  const vals: any[] = [];

  if (settings.hwidPolicy !== undefined) {
    fields.push('hwid_policy = ?');
    vals.push(settings.hwidPolicy);
  }
  if (settings.allowVpn !== undefined) {
    fields.push('allow_vpn = ?');
    vals.push(settings.allowVpn ? 1 : 0);
  }
  if (settings.maxAccountsPerHwid !== undefined) {
    fields.push('max_accounts_per_hwid = ?');
    vals.push(settings.maxAccountsPerHwid);
  }
  if (settings.discordWebhookUrl !== undefined) {
    fields.push('discord_webhook_url = ?');
    vals.push(settings.discordWebhookUrl || null);
  }
  if (settings.telegramWebhookToken !== undefined) {
    fields.push('telegram_webhook_token = ?');
    vals.push(settings.telegramWebhookToken || null);
  }
  if (settings.telegramChatId !== undefined) {
    fields.push('telegram_chat_id = ?');
    vals.push(settings.telegramChatId || null);
  }
  if (settings.webhookAlertsEnabled !== undefined) {
    fields.push('webhook_alerts_enabled = ?');
    vals.push(settings.webhookAlertsEnabled ? 1 : 0);
  }

  if (fields.length === 0) return true;

  vals.push(projectId);
  const res: any = await query(`UPDATE portal_projects SET ${fields.join(', ')} WHERE id = ?`, vals);
  return res.affectedRows > 0;
}

/** Regenerate the project's Agent API key. Returns the new key. */
export async function rotateProjectApiKey(projectId: number): Promise<string> {
  const apiKey = `flv_live_${Math.random().toString(36).substring(2, 12)}_${Date.now().toString(36)}`;
  await query('UPDATE portal_projects SET api_key = ? WHERE id = ?', [apiKey, projectId]);
  return apiKey;
}

export async function getResourcesByServer(serverId: number): Promise<ResourceRecord[]> {
  const rows = await query('SELECT * FROM portal_resources WHERE server_id = ? ORDER BY name ASC', [serverId]);
  const list = rows as ResourceRecord[];
  if (list.length === 0) {
    // Return standard default FloV:MP resource pack if none registered yet
    return [
      { id: 1, server_id: serverId, name: 'flovmp-core', type: 'gamemode', status: 'running', version: '1.0.4', author: 'FloV:MP' },
      { id: 2, server_id: serverId, name: 'flovmp-factions', type: 'script', status: 'running', version: '1.0.0', author: 'FloV:MP' },
      { id: 3, server_id: serverId, name: 'flovmp-vehicles', type: 'vehicles', status: 'running', version: '1.2.0', author: 'FloV:MP' },
      { id: 4, server_id: serverId, name: 'flovmp-hud', type: 'ui', status: 'running', version: '1.0.0', author: 'FloV:MP' },
      { id: 5, server_id: serverId, name: 'flovmp-map-stream', type: 'map', status: 'running', version: '2.0.0', author: 'FloV:MP' },
    ];
  }
  return list;
}

export async function setResourceStatus(
  serverId: number,
  name: string,
  status: 'running' | 'stopped' | 'failed'
): Promise<boolean> {
  const now = new Date().toISOString();
  const res: any = await query(
    'INSERT INTO portal_resources (server_id, name, status, started_at) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE status = VALUES(status), started_at = VALUES(started_at)',
    [serverId, name, status, status === 'running' ? now : null]
  );
  return res.affectedRows > 0;
}

export async function recordServerCrash(
  serverId: number,
  incidentId: string,
  reason: string,
  stackTrace?: string,
  memoryMb: number = 0
): Promise<number> {
  const res: any = await query(
    'INSERT INTO portal_server_crashes (server_id, incident_id, reason, stack_trace, memory_mb) VALUES (?, ?, ?, ?, ?)',
    [serverId, incidentId, reason, stackTrace || null, memoryMb]
  );
  return res.insertId;
}
