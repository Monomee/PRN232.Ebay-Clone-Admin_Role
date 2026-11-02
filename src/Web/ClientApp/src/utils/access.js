// A page is open to its system roles, or to a custom admin role that grants the matching API policy
// (`permissions` comes from /api/Auth/me, one entry per permission claim in the JWT).
export function canAccess(user, roles = [], permission) {
  if (roles.length === 0) return true;
  if (roles.includes(user?.role)) return true;
  return !!permission && (user?.permissions || []).includes(permission);
}
