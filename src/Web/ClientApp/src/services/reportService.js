import { apiRequest } from './httpClient';

const REPORTS_BASE = '/api/Stats/reports';

const reportService = {
    getRevenue: async (startDate, endDate) => {
        return await apiRequest(`${REPORTS_BASE}/revenue`, {
            query: { startDate, endDate }
        });
    },

    getUserGrowth: async (startDate, endDate) => {
        return await apiRequest(`${REPORTS_BASE}/users`, {
            query: { startDate, endDate }
        });
    },

    getOrderStats: async (startDate, endDate) => {
        return await apiRequest(`${REPORTS_BASE}/orders`, {
            query: { startDate, endDate }
        });
    }
};

export default reportService;
