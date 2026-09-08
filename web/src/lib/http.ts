import { NextRequest, NextResponse } from 'next/server';

/** Thrown by readJson when the request body is missing or not valid JSON. */
export class BadJsonError extends Error {
  constructor() {
    super('Invalid or missing JSON body');
    this.name = 'BadJsonError';
  }
}

/**
 * Parse a JSON request body, throwing BadJsonError (→ 400) instead of a raw
 * SyntaxError (→ 500) when the payload is malformed. Every route that reads a
 * body should use this and map BadJsonError to a 400 response.
 */
export async function readJson<T = Record<string, unknown>>(req: NextRequest): Promise<T> {
  try {
    const body = await req.json();
    if (body === null || typeof body !== 'object') throw new BadJsonError();
    return body as T;
  } catch {
    throw new BadJsonError();
  }
}

export function badRequest(message = 'Некорректное тело запроса') {
  return NextResponse.json({ error: message }, { status: 400 });
}

export function unauthorized(message = 'Требуется авторизация') {
  return NextResponse.json({ error: message }, { status: 401 });
}

export function forbidden(message = 'Недостаточно прав') {
  return NextResponse.json({ error: message }, { status: 403 });
}
