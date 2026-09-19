import crypto from 'crypto';

function requiredSecret(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is not configured; refusing to issue or sign a license`);
  return value;
}

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
  lifetime: { id: 'lifetime', name: 'FloV:MP Lifetime', priceMonthly: 25000, priceHalfYear: 25000, priceYearly: 25000, maxPlayers: 1500, features: { voice3D: true, antiCheat: true, customDlcStreaming: true, fastDlCdn: true, unlimitedEntities: true, customLauncherBranding: true, prioritySupport: true } },
  indie: { id: 'indie', name: 'Инди (Разработка)', priceMonthly: 0, priceHalfYear: 0, priceYearly: 0, maxPlayers: 128, features: { voice3D: true, antiCheat: true, customDlcStreaming: true, fastDlCdn: true, unlimitedEntities: false, customLauncherBranding: false, prioritySupport: false } },
  business: { id: 'business', name: 'RP Проект', priceMonthly: 14900, priceHalfYear: 12900, priceYearly: 9900, maxPlayers: 1500, features: { voice3D: true, antiCheat: true, customDlcStreaming: true, fastDlCdn: true, unlimitedEntities: false, customLauncherBranding: true, prioritySupport: true } },
  enterprise: { id: 'enterprise', name: 'Enterprise / 5000+ Масштаб', priceMonthly: 49000, priceHalfYear: 42000, priceYearly: 36000, maxPlayers: 5000, features: { voice3D: true, antiCheat: true, customDlcStreaming: true, fastDlCdn: true, unlimitedEntities: true, customLauncherBranding: true, prioritySupport: true } },
};

// New keys carry 128 bits of entropy. The legacy shape remains accepted so existing customers are not invalidated.
const KEY_PATTERN = /^FLV-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/i;
const STRONG_KEY_PATTERN = /^FLV-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}$/i;

export function generateLicenseKey(prefix = 'FLV'): string {
  if (prefix !== 'FLV') throw new Error('Unsupported license prefix');
  const bytes = crypto.randomBytes(16).toString('hex').toUpperCase();
  return `${prefix}-${bytes.slice(0, 8)}-${bytes.slice(8, 16)}-${bytes.slice(16, 24)}-${bytes.slice(24, 32)}`;
}

export function isValidLicenseKeyFormat(key: string): boolean {
  const value = key.trim();
  return STRONG_KEY_PATTERN.test(value) || KEY_PATTERN.test(value);
}

export function signLicensePayload(data: Record<string, any>): string {
  const serialized = JSON.stringify(data, Object.keys(data).sort());
  return crypto.createHmac('sha256', requiredSecret('LICENSE_SIGNING_SECRET')).update(serialized).digest('hex');
}

export function verifyLicenseSignature(data: Record<string, any>, signature: string): boolean {
  const expected = signLicensePayload(data);
  const actual = Buffer.from(signature, 'utf8');
  const wanted = Buffer.from(expected, 'utf8');
  return actual.length === wanted.length && crypto.timingSafeEqual(actual, wanted);
}

/** Создаёт RSA-подписанный license.flv для серверного рантайма. */
export function createSignedLicenseFlv(params: {
  licenseKey: string;
  project: string;
  issuedTo: string;
  plan?: string;
  maxPlayers?: number;
  maxServers?: number;
  days?: number;
  expiresAt?: string | Date;
}): { payload: Record<string, any>; flvJson: string } {
  if (!isValidLicenseKeyFormat(params.licenseKey)) throw new Error('Invalid license key format');
  const { licenseKey, project, issuedTo, plan = 'enterprise', maxPlayers = 5000, maxServers = 10, days = 365 } = params;
  const now = new Date();
  const expiresAt = params.expiresAt ? new Date(params.expiresAt) : new Date(now.getTime() + days * 24 * 60 * 60 * 1000);
  if (!Number.isFinite(expiresAt.getTime()) || expiresAt <= now) {
    throw new Error('License expiry must be a valid future date');
  }
  const watermark = crypto.createHash('sha256').update(`${issuedTo}|${licenseKey}`).digest('hex').substring(0, 16);
  const payload = { licenseKey, project, issuedTo, plan, maxPlayers: Number(maxPlayers), maxServers: Number(maxServers), issuedAt: now.toISOString(), expiresAt: expiresAt.toISOString(), watermark };
  const payloadBuffer = Buffer.from(JSON.stringify(payload));
  const privateKey = requiredSecret('FLOVMP_AUTHORITY_PRIVATE_KEY');
  const sign = crypto.createSign('SHA256');
  sign.update(payloadBuffer);
  sign.end();
  const signature = sign.sign(privateKey);
  return { payload, flvJson: JSON.stringify({ payload_b64: payloadBuffer.toString('base64'), signature: signature.toString('base64') }, null, 2) };
}

/** Подписанный короткоживущий lease для online-проверки сервером. */
export function createSignedLicenseLease(params: {
  licenseKey: string;
  validUntil: string;
  reason?: string;
}): { leasePayloadB64: string; leaseSignature: string } {
  if (!isValidLicenseKeyFormat(params.licenseKey)) throw new Error('Invalid license key format');
  const payload = {
    licenseKey: params.licenseKey,
    valid: true,
    issuedAt: new Date().toISOString(),
    validUntil: params.validUntil,
    reason: params.reason || '',
  };
  const payloadBuffer = Buffer.from(JSON.stringify(payload));
  const sign = crypto.createSign('SHA256');
  sign.update(payloadBuffer);
  sign.end();
  const signature = sign.sign(requiredSecret('FLOVMP_AUTHORITY_PRIVATE_KEY'));
  return {
    leasePayloadB64: payloadBuffer.toString('base64'),
    leaseSignature: signature.toString('base64'),
  };
}
