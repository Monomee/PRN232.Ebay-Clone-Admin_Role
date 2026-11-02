import endpoints from "./endpoints";
import { apiRequest } from "./httpClient";

export function getAuditLogs(query) {
  return apiRequest(endpoints.auditLogs, { query });
}

// { total, critical } over every log matching the same filter
export function getAuditLogSummary(query) {
  return apiRequest(`${endpoints.auditLogs}/summary`, { query });
}

