import crypto from 'crypto';

const SIGNING_SECRET = process.env.LICENSE_SIGNING_SECRET || 'flovmp_master_cryptographic_engine_key_2026';

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
    maxPlayers: 1500,
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
    name: 'Enterprise / 5000+ Масштаб',
    priceMonthly: 49000,
    priceHalfYear: 42000,
    priceYearly: 36000,
    maxPlayers: 5000,
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

export const DEFAULT_AUTHORITY_PRIVATE_KEY = `-----BEGIN PRIVATE KEY-----
MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC4qKtDFKNfeFxL
zRdUUDyg5k8R9rZnNI4yPUHdCClyaNT+Uj75M4qma+HH8kaV5C2HW5DKAnymsVi4
PfBM29HhlM7yWtI1LhP8n6AhBBmJdmh8kJes6d/cHPHl7/X5cK2EmTIFnJBQUjRe
z94gS2eBuqx0enOxPBQhmGGNm/djvYb11Xq9Fxde7adlFsDUCM4xFHoGc2HkHJSO
3lpRK3Zq8BaM93lhkwU29pW9ymieNuErhzGFXVbdrQm5cT/eA1gYET7f/oj6U35/
KUlCQqPmTPsxMW7vOGUhTs18cDXSF0qJt0tGIOMBRdX1uEovWatidYlRKRRy2C6c
5S7HE5AdAgMBAAECggEABPoy55N6VdvyLO7hxpmFENc0YWkTgJOnk44YJOOP7nAJ
8bL41JHBlnvI8bFDms20NnZO4EsR3hJgsFKnFANG1HxE2b599QYBbulEkS9BmvVw
mo7xL618Jbw3/vkpWahiXPUeJo2STD/y/m0+8QLnTtVfyaH1VV0ns8IeOwltsSgC
bZU2XITmJbmT2qpJi3rfxdDDhoJ5k7zz5u+3t4954+uTVyprR0LaTE55SC+vNDsQ
vmdbr0hK1rObVokAE9h7UwIM9XG4/Wk4xz7I87SMppaeSS1OGlEb9IbpTpKpV/Zz
81Epm9dXjVDErDmy6aLppnWajRCdBnLtyHDkFTGwtwKBgQDgmT/nLZljXqJMUjg6
os42RXiflDY/KAHYV4wh6UDjyB0V1aN3mx7AdfcJiO4k7W2HFOV6iTxV1iFY2H3+
uX3T+l5vVZHv+o9WEZyxyxwFvWqGKWlIybw8P7QXNnFukz91sojbJ5AWQrNEvM22
yqA8Gvi/I3/Ryu3eJF9wJlNEKwKBgQDSeej8Tbt8skuPygbaka83hYICHVDnk6ro
3giUPpYXisXHANZMKkjbTVAutdfux7eaXVZql47oZOGx32Eo0YvwvAp94SHULeRy
jxa+YLAqyLEO0clEj9M3f5X0KABib6NPjFBuq28xqG0I7TbtoS5X0iNyCwBqknlQ
7KZrpjfw1wKBgQDSZLzIAp89xtiiRiMGSpyBmnJ0ipKGdyPDVb+fxLFUr5EPcyG+
WUFlaRwRgoQTc5a2g4y0TPqILh7u616bz4dfm5n7EV20QDMlnTn8ExgdCGNRalmG
JfR/O+2oEQgRXT6FMsmhAl6ne7QTIApUwVt48osyAj8qd7576fa9SCmYIQKBgQDM
aNl9L4EGeaIo42wEmbgxk9fPdek8/ozd28U5NA9QjdXGj2mQTwCy+0MEGla77/rA
UCW9H7QKvu7ycJ9LpTbjdbw6xIq2JlvXZmAQRJbS9lX+rn9ptsTLAX4AwVieQQ26
E+uJj7VN5OT/5mGSJxd68vq5pzWgSuDHVw3JPoTVwQKBgB+quryTHR+ahCVOhPuG
YjDNCxRkGgK5EJ1fhfyPcrY44MNE3bvb3GNh4qhmxhLowXc7haGXM6VIMP7q3l64
389m1KvGq8xNHrpFphaILfIbbD9dno6hQiU26rlI8+KrJC1lPIpVkoL28cWjK/pM
LxTCB11fmofkZZt3akbz0/em
-----END PRIVATE KEY-----`;

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

/**
 * Создаёт криптографически подписанный RSA-2048 файл `license.flv`,
 * валидируемый классом `FloVMP.Core.Licensing.LicenseFile` в рантайме платформы.
 */
export function createSignedLicenseFlv(params: {
  licenseKey: string;
  project: string;
  issuedTo: string;
  plan?: string;
  maxPlayers?: number;
  maxServers?: number;
  days?: number;
}): {
  payload: Record<string, any>;
  flvJson: string;
} {
  const {
    licenseKey,
    project,
    issuedTo,
    plan = 'enterprise',
    maxPlayers = 5000,
    maxServers = 10,
    days = 365,
  } = params;

  const now = new Date();
  const expiresAt = new Date(now.getTime() + days * 24 * 60 * 60 * 1000);

  const watermark = crypto
    .createHash('sha256')
    .update(`${issuedTo}|${licenseKey}`)
    .digest('hex')
    .substring(0, 16);

  const payload = {
    licenseKey,
    project,
    issuedTo,
    plan,
    maxPlayers: Number(maxPlayers),
    maxServers: Number(maxServers),
    issuedAt: now.toISOString(),
    expiresAt: expiresAt.toISOString(),
    watermark,
  };

  const payloadBuffer = Buffer.from(JSON.stringify(payload));

  const privateKey = process.env.FLOVMP_AUTHORITY_PRIVATE_KEY || DEFAULT_AUTHORITY_PRIVATE_KEY;
  const sign = crypto.createSign('SHA256');
  sign.update(payloadBuffer);
  sign.end();
  const signature = sign.sign(privateKey);

  const flvJson = JSON.stringify(
    {
      payload_b64: payloadBuffer.toString('base64'),
      signature: signature.toString('base64'),
    },
    null,
    2
  );

  return { payload, flvJson };
}
