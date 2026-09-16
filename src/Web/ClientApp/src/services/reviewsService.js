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
    getFlaggedReviews: async (pageNumber = 1, pageSize = 10) => {
        try {
            const response = await axios.get(`${ENDPOINTS.REVIEWS.FLAGGED}?pageNumber=${pageNumber}&pageSize=${pageSize}`);
            return response.data;
        } catch (error) {
            console.error("Error fetching flagged reviews:", error);
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
