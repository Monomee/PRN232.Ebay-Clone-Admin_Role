import { isSessionExpired } from "./sessionGuard";

test("401 from an API call ends the session", () => {
  expect(isSessionExpired(401, "/api/Users/summary", "/users")).toBe(true);
  expect(isSessionExpired(401, "https://localhost:5001/api/Disputes/1", "/disputes/1")).toBe(true);
});

test("auth endpoints, other statuses and the login page itself do not redirect", () => {
  expect(isSessionExpired(401, "/api/Auth/me", "/")).toBe(false);
  expect(isSessionExpired(401, "/api/auth/verify-2fa", "/login")).toBe(false);
  expect(isSessionExpired(403, "/api/Users/summary", "/users")).toBe(false);
  expect(isSessionExpired(401, "/api/Users/summary", "/login")).toBe(false);
  expect(isSessionExpired(401, "/hubs/dispute/negotiate", "/disputes")).toBe(false);
});
