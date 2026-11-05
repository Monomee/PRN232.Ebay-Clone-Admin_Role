// The server ends a session mid-use (logout elsewhere, ban, role change, 2FA not set up): every API call then
// answers 401. Send the user to /login once instead of leaving each page to show a broken state.
// /api/Auth/* is excluded: those calls report their own outcome (e.g. /me is 401 for a visitor).
export function isSessionExpired(status, url, pathname) {
  if (status !== 401 || pathname.startsWith("/login")) return false;
  const path = new URL(url, "http://local").pathname;
  return path.startsWith("/api/") && !path.toLowerCase().startsWith("/api/auth/");
}

let redirecting = false;
function check(status, url) {
  if (redirecting || !isSessionExpired(status, url || "", window.location.pathname)) return;
  redirecting = true;
  window.location.assign("/login");
}

export function guardAxios(instance) {
  instance.interceptors.response.use(undefined, (error) => {
    check(error?.response?.status, error?.config && instance.getUri(error.config)); // getUri adds baseURL
    return Promise.reject(error);
  });
  return instance;
}

export function installSessionGuard(axios) {
  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input, init) => {
    const response = await originalFetch(input, init);
    check(response.status, typeof input === "string" ? input : input?.url);
    return response;
  };
  guardAxios(axios);
}
