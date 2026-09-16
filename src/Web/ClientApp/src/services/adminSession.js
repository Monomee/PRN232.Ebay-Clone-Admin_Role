const ADMIN_ID_KEY = "ebayclone_admin_id";

export function getCurrentAdminId() {
  const rawValue = window.localStorage.getItem(ADMIN_ID_KEY);
  const parsed = Number(rawValue);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

export function setCurrentAdminId(adminId) {
  const parsed = Number(adminId);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error("Admin ID must be a positive integer.");
  }

  window.localStorage.setItem(ADMIN_ID_KEY, String(parsed));
}

