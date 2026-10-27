import axios from 'axios';
import { ENDPOINTS } from './endpoints';

export const sensitiveLogService = {
    // GET /api/sensitive-logs
    getSensitiveLogs: async ({
        pageNumber = 1,
        pageSize = 20,
        actorUsername = '',
        action = '',
        classification = '',
        searchTerm = '',
        fromDate = '',
        toDate = ''
    } = {}) => {
        try {
            const params = new URLSearchParams();
            if (pageNumber) params.append('pageNumber', pageNumber);
            if (pageSize) params.append('pageSize', pageSize);
            if (actorUsername) params.append('actorUsername', actorUsername);
            if (action && action !== 'All') params.append('action', action);
            if (classification && classification !== 'All') params.append('classification', classification);
            if (searchTerm) params.append('searchTerm', searchTerm);
            if (fromDate) params.append('fromDate', fromDate);
            if (toDate) params.append('toDate', toDate);

            const response = await axios.get(`${ENDPOINTS.SENSITIVE_LOGS.BASE}?${params.toString()}`);
            return response.data;
        } catch (error) {
            console.error("Error fetching sensitive data logs:", error);
            throw error;
        }
    },

    // GET /api/sensitive-logs/summary
    getSummary: async () => {
        try {
            const response = await axios.get(ENDPOINTS.SENSITIVE_LOGS.SUMMARY);
            return response.data;
        } catch (error) {
            console.error("Error fetching sensitive logs summary:", error);
            throw error;
        }
    },

    // POST /api/sensitive-logs/{id}/decrypt
    decryptLog: async (id) => {
        try {
            const response = await axios.post(ENDPOINTS.SENSITIVE_LOGS.DECRYPT(id));
            return response.data;
        } catch (error) {
            console.error("Error decrypting sensitive data log:", error);
            throw error;
        }
    }
};

export default sensitiveLogService;
