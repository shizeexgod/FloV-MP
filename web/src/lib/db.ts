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
}

const DB_DIR = path.join(process.cwd(), 'data');
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

  return [];
}
