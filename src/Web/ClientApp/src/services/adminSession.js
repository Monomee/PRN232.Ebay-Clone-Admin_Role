const ADMIN_ID_KEY = "ebayclone_admin_id";

// Id of the signed-in admin, stored by AuthContext from /api/Auth/me. null when unknown;
// never a default admin (z8rp3fcg6y: everything used to be attributed to admin #1).
export function getCurrentAdminId() {
  const rawValue = window.localStorage.getItem(ADMIN_ID_KEY);
  const parsed = Number(rawValue);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export function setCurrentAdminId(adminId) {
  const parsed = Number(adminId);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error("Admin ID must be a positive integer.");
  }

  window.localStorage.setItem(ADMIN_ID_KEY, String(parsed));
}

