import axios from "axios";
import { ENDPOINTS } from "./endpoints";
import endpoints from "./endpoints";
import { apiRequest } from "./httpClient";

// Ensure cookies (auth_token) are sent with every axios request
axios.defaults.withCredentials = true;

// axios-based service (used by HEAD branch pages)
export const userService = {
  getUsers: async (params) => {
    const response = await axios.get(ENDPOINTS.USERS.BASE, { params });
    return response.data;
  },

  getUserById: async (id) => {
    const response = await axios.get(ENDPOINTS.USERS.DETAIL(id));
    return response.data;
  },

  approveUser: async (id, adminId) => {
    const response = await axios.post(ENDPOINTS.USERS.APPROVE(id), { adminId });
    return response.data;
  },

  banUser: async (id, reason, adminId) => {
    const response = await axios.post(ENDPOINTS.USERS.BAN(id), { reason, adminId });
    return response.data;
  },

  unbanUser: async (id, adminId) => {
    const response = await axios.post(ENDPOINTS.USERS.UNBAN(id), { adminId });
    return response.data;
  }
};

// apiRequest-based functions (used by main branch pages)
export function getUsers(query) {
  return apiRequest(endpoints.users, { query });
}

export function getUserById(userId) {
  return apiRequest(`${endpoints.users}/${userId}`);
}

export function approveUser(userId, adminId) {
  return apiRequest(`${endpoints.users}/${userId}/approve`, {
    method: "POST",
    body: { adminId },
  });
}

export function rejectUser(userId, reason, adminId) {
  return apiRequest(`${endpoints.users}/${userId}/reject`, {
    method: "POST",
    body: { reason, adminId },
  });
}

export function banUser(userId, reason, adminId) {
  return apiRequest(`${endpoints.users}/${userId}/ban`, {
    method: "POST",
    body: { reason, adminId },
  });
}

export function unbanUser(userId, adminId) {
  return apiRequest(`${endpoints.users}/${userId}/unban`, {
    method: "POST",
    body: { adminId },
  });
}

export function updateUserStatus(userId, status, adminId) {
  return apiRequest(`${endpoints.users}/${userId}/status`, {
    method: "PUT",
    body: { status, adminId },
  });
}

export function generateMockUsers() {
  return apiRequest(`/api/Mocking/generate-mock-users`, {
    method: "POST",
    body: {}
  });
}
