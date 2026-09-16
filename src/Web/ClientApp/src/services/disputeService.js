import axios from 'axios';

const API_BASE = '/api';

// T?o axios instance riêng ho?c set defaults ð? dính kèm ch?ng ch? (credentials/cookies)
const api = axios.create({
    baseURL: API_BASE,
    withCredentials: true // R?t quan tr?ng ð? g?i kèm HttpOnly Cookie (Auth)
});

export const disputeService = {
    // ===============================
    // 1. Dashboard Statistics
    // ===============================
    
    getStatistics: async () => {
        try {
            const response = await api.get(`/disputes/statistics`);
            return response.data;
        } catch (error) {
            console.error('Error fetching dispute statistics:', error);
            throw error;
        }
    },

    // ===============================
    // 2. Disputes Docket (List)
    // ===============================
    
    getDisputes: async (params = {}) => {
        try {
            const response = await api.get(`/disputes`, { params });
            return response.data;
        } catch (error) {
            console.error('Error fetching disputes:', error);
            throw error;
        }
    },

    // ===============================
    // 3. Dispute Detail
    // ===============================
    
    getDisputeDetail: async (id) => {
        try {
            const response = await api.get(`/disputes/${id}`);
            return response.data;
        } catch (error) {
            console.error('Error fetching dispute detail:', error);
            throw error;
        }
    },

    // ===============================
    // 4. Dispute Actions
    // ===============================
    
    assignDispute: async (id) => {
        try {
            const response = await api.post(`/disputes/${id}/assign`);
            return response.data;
        } catch (error) {
            console.error('Error assigning dispute:', error);
            throw error;
        }
    },

    resolveDispute: async (id, payload) => {
        try {
            const response = await api.post(`/disputes/${id}/resolve`, payload);
            return response.data;
        } catch (error) {
            console.error('Error resolving dispute:', error);
            throw error;
        }
    }
};