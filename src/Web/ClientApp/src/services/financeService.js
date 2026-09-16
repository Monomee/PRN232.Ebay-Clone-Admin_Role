import { apiRequest } from './httpClient';
import endpoints from './endpoints';

const financeService = {
    getSellerWallets: async () => {
        return await apiRequest(endpoints.wallets);
    },

    settleFunds: async () => {
        return await apiRequest(`${endpoints.financials}/trigger-settlement`, {
            method: 'POST'
        });
    },

    getPendingSettlementOrders: async () => {
        return await apiRequest(`${endpoints.financials}/settlement-orders`);
    },

    settleOrder: async (id) => {
        return await apiRequest(`${endpoints.financials}/settle-order/${id}`, {
            method: 'POST'
        });
    },

    getSellerPendingFunds: async (sellerId) => {
        return await apiRequest(`${endpoints.financials}/pending/${sellerId}`);
    },

    // ── Automated Payout Engine ─────────────────────────────────────────────
    runPayoutEngine: async () => {
        return await apiRequest(`${endpoints.financials}/payouts/run`, { method: 'POST' });
    },

    releaseHold: async (payoutTransactionId) => {
        return await apiRequest(`${endpoints.financials}/payouts/${payoutTransactionId}/release-hold`, {
            method: 'POST'
        });
    },

    getPayoutHistory: async (groupBy = 'day') => {
        return await apiRequest(`${endpoints.financials}/payouts/history?groupBy=${groupBy}`);
    },

    getPayoutExceptions: async () => {
        return await apiRequest(`${endpoints.financials}/payouts/exceptions`);
    },

    getPayoutConfig: async () => {
        return await apiRequest(`${endpoints.financials}/payouts/config`);
    },

    updatePayoutConfig: async (config) => {
        return await apiRequest(`${endpoints.financials}/payouts/config`, {
            method: 'PUT',
            body: config
        });
    }
};

export default financeService;
