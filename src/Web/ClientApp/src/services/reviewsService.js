import axios from 'axios';
import { ENDPOINTS } from './endpoints';

export const reviewsService = {
    // POST /api/reviews
    createReview: async (payload) => {
        try {
            const response = await axios.post(ENDPOINTS.REVIEWS.BASE, payload);
            return response.data;
        } catch (error) {
            console.error("Error submitting review:", error);
            throw error;
        }
    },

    // GET /api/reviews/flagged
    getFlaggedReviews: async ({ pageNumber = 1, pageSize = 10, status = '', reason = '', searchTerm = '', rating = '' } = {}) => {
        try {
            const params = new URLSearchParams();
            if (pageNumber) params.append('pageNumber', pageNumber);
            if (pageSize) params.append('pageSize', pageSize);
            if (status && status !== 'All') params.append('status', status);
            if (reason && reason !== 'All') params.append('reason', reason);
            if (searchTerm) params.append('searchTerm', searchTerm);
            if (rating) params.append('rating', rating);

            const response = await axios.get(`${ENDPOINTS.REVIEWS.FLAGGED}?${params.toString()}`);
            return response.data;
        } catch (error) {
            console.error("Error fetching flagged reviews:", error);
            throw error;
        }
    },

    // GET /api/reviews/statistics
    getReviewStatistics: async () => {
        try {
            const response = await axios.get(ENDPOINTS.REVIEWS.STATISTICS);
            return response.data;
        } catch (error) {
            console.error("Error fetching review statistics:", error);
            throw error;
        }
    },

    // GET /api/reviews/seller-feedbacks
    getSellerFeedbacks: async ({ pageNumber = 1, pageSize = 10, searchTerm = '', tier = '' } = {}) => {
        try {
            const params = new URLSearchParams();
            if (pageNumber) params.append('pageNumber', pageNumber);
            if (pageSize) params.append('pageSize', pageSize);
            if (searchTerm) params.append('searchTerm', searchTerm);
            if (tier && tier !== 'All') params.append('tier', tier);

            const response = await axios.get(`${ENDPOINTS.REVIEWS.FEEDBACKS}?${params.toString()}`);
            return response.data;
        } catch (error) {
            console.error("Error fetching seller feedbacks:", error);
            throw error;
        }
    },

    // PUT /api/reviews/{id}/status
    updateReviewStatus: async (id, payload) => {
        try {
            const response = await axios.put(ENDPOINTS.REVIEWS.STATUS(id), payload);
            return response.data;
        } catch (error) {
            console.error("Error updating review status:", error);
            throw error;
        }
    },

    // POST /api/reviews/{id}/reply
    replyToReview: async (id, payload) => {
        try {
            const response = await axios.post(ENDPOINTS.REVIEWS.REPLY(id), payload);
            return response.data;
        } catch (error) {
            console.error("Error responding to review:", error);
            throw error;
        }
    },

    // POST /api/reviews/{id}/report
    reportReview: async (id, payload) => {
        try {
            const response = await axios.post(ENDPOINTS.REVIEWS.REPORT(id), payload);
            return response.data;
        } catch (error) {
            console.error("Error reporting review:", error);
            throw error;
        }
    }
};
