import endpoints from "./endpoints";
import { apiRequest } from "./httpClient";

export function getAdminRoles() {
  return apiRequest(`${endpoints.adminRoles}/roles`);
}

export function createRole(payload) {
  return apiRequest(`${endpoints.adminRoles}/roles`, {
    method: "POST",
    body: payload,
  });
}

export function getAdminUsers(query) {
  return apiRequest(`${endpoints.adminRoles}/users`, { query });
}

export function createAdminUser(payload) {
  return apiRequest(`${endpoints.adminRoles}/users`, {
    method: "POST",
    body: payload,
  });
}

export function assignRole(payload) {
  return apiRequest(`${endpoints.adminRoles}/users/assign`, {
    method: "POST",
    body: payload,
  });
}

