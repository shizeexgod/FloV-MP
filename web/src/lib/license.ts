import crypto from 'crypto';

export function generateLicenseKey(prefix = 'FLV'): string {
  const seg1 = crypto.randomBytes(2).toString('hex').toUpperCase();
  const seg2 = crypto.randomBytes(2).toString('hex').toUpperCase();
  const seg3 = crypto.randomBytes(2).toString('hex').toUpperCase();
  return `${prefix}-${seg1}-${seg2}-${seg3}`;
}

export function isValidLicenseKeyFormat(key: string): boolean {
  return /^FLV-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/i.test(key.trim()) || key.startsWith('FLV-');
}
