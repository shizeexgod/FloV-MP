import { getSessionUser, type TokenPayload } from '@/lib/auth';

/** Platform-level roles that may touch the admin surface and every project. */
export const PLATFORM_ADMIN_ROLES = ['owner', 'admin'];

export class AuthError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.name = 'AuthError';
    this.status = status;
  }
}

/** Returns the session or throws AuthError(401). */
export function requireUser(): TokenPayload {
  const s = getSessionUser();
  if (!s) throw new AuthError(401, 'Требуется авторизация');
  return s;
}

/** Returns the session or throws AuthError(403) unless it is a platform admin. */
export function requirePlatformAdmin(): TokenPayload {
  const s = requireUser();
  if (!PLATFORM_ADMIN_ROLES.includes(s.role)) {
    throw new AuthError(403, 'Доступ только для администраторов платформы');
  }
  return s;
}

export function isPlatformAdmin(s: TokenPayload | null): boolean {
  return !!s && PLATFORM_ADMIN_ROLES.includes(s.role);
}

/**
 * A project may be managed by its owner (project.user_id) or any platform admin.
 * Team roles (project_lead / developer) will slot in here once the schema carries
 * a project_members table — the call sites do not change.
 */
export function assertProjectAccess(s: TokenPayload, project: { user_id?: number } | null | undefined): void {
  if (!project) throw new AuthError(404, 'Проект не найден');
  if (isPlatformAdmin(s)) return;
  if (project.user_id != null && Number(project.user_id) === Number(s.userId)) return;
  throw new AuthError(403, 'Нет доступа к этому проекту');
}
