import crypto from 'crypto';

const SIGNING_SECRET = process.env.LICENSE_SIGNING_SECRET || 'flovmp_master_cryptographic_engine_key_2026_derzhava';

export interface PlanDetails {
  id: string;
  name: string;
  priceMonthly: number;
  priceHalfYear: number;
  priceYearly: number;
  maxPlayers: number;
  features: {
    voice3D: boolean;
    antiCheat: boolean;
    customDlcStreaming: boolean;
    fastDlCdn: boolean;
    unlimitedEntities: boolean;
    customLauncherBranding: boolean;
    prioritySupport: boolean;
  };
}

export const PLANS: Record<string, PlanDetails> = {
  indie: {
    id: 'indie',
    name: 'Инди (Разработка)',
    priceMonthly: 0,
    priceHalfYear: 0,
    priceYearly: 0,
    maxPlayers: 128,
    features: {
      voice3D: true,
      antiCheat: true,
      customDlcStreaming: true,
      fastDlCdn: true,
      unlimitedEntities: false,
      customLauncherBranding: false,
      prioritySupport: false,
    },
  },
  business: {
    id: 'business',
    name: 'RP Проект',
    priceMonthly: 14900,
    priceHalfYear: 12900,
    priceYearly: 9900,
    maxPlayers: 512,
    features: {
      voice3D: true,
      antiCheat: true,
      customDlcStreaming: true,
      fastDlCdn: true,
      unlimitedEntities: false,
      customLauncherBranding: true,
      prioritySupport: true,
    },
  },
  enterprise: {
    id: 'enterprise',
    name: 'Enterprise Франшиза',
    priceMonthly: 49000,
    priceHalfYear: 42000,
    priceYearly: 36000,
    maxPlayers: 1500,
    features: {
      voice3D: true,
      antiCheat: true,
      customDlcStreaming: true,
      fastDlCdn: true,
      unlimitedEntities: true,
      customLauncherBranding: true,
      prioritySupport: true,
    },
  },
};

export function generateLicenseKey(prefix = 'FLV'): string {
  const seg1 = crypto.randomBytes(2).toString('hex').toUpperCase();
  const seg2 = crypto.randomBytes(2).toString('hex').toUpperCase();
  const seg3 = crypto.randomBytes(2).toString('hex').toUpperCase();
  return `${prefix}-${seg1}-${seg2}-${seg3}`;
}

export function isValidLicenseKeyFormat(key: string): boolean {
  return /^FLV-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/i.test(key.trim()) || key.startsWith('FLV-');
}

export function signLicensePayload(data: Record<string, any>): string {
  const serialized = JSON.stringify(data, Object.keys(data).sort());
  return crypto.createHmac('sha256', SIGNING_SECRET).update(serialized).digest('hex');
}

export function verifyLicenseSignature(data: Record<string, any>, signature: string): boolean {
  const expected = signLicensePayload(data);
  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(signature));
}
