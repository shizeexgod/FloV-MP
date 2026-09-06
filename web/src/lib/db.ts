import mysql, { Pool } from 'mysql2/promise';
import fs from 'fs';
import path from 'path';

interface MockStore {
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
      return JSON.parse(data);
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
  if (s.includes('from portal_users') && s.includes('id =')) {
    const id = Number(params[0]);
    const user = store.users.find((u) => u.id === id);
    return user ? [user] : [];
  }

  // 3. Insert user
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

  // 4. Find license by id (and optional user_id)
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

  // 5. Find licenses by user_id
  if (s.includes('from portal_licenses') && /user_id\s*=/i.test(s)) {
    const userId = Number(params[0]);
    return store.licenses.filter((l) => l.user_id === userId);
  }

  // 6. Find license by key
  if (s.includes('from portal_licenses') && s.includes('license_key =')) {
    const key = String(params[0]);
    return store.licenses.filter((l) => l.license_key === key);
  }

  // 7. Update license bound_ip
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

  // 8. Update last_verified_at
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

  // 9. Create new license
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

  return [];
}
