import endpoints from "./endpoints";
import { apiRequest } from "./httpClient";

export function getAuditLogs(query) {
  return apiRequest(endpoints.auditLogs, { query });
}

