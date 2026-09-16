import axios from 'axios';
import { ENDPOINTS } from './endpoints';

export const productService = {

    // ===============================
    // 1. Public Products
    // ===============================

    // GET /api/products
    getAllProducts: async () => {
        try {
            const response = await axios.get(ENDPOINTS.PRODUCTS.BASE);
            return response.data;
        } catch (error) {
            console.error("Error fetching product list:", error);
            throw error;
        }
    },

    // GET /api/products/{id}
    getProductById: async (id) => {
        try {
            const response = await axios.get(ENDPOINTS.PRODUCTS.DETAIL(id));
            return response.data;
        } catch (error) {
            console.error("Error fetching product details:", error);
            throw error;
        }
    },

    // POST /api/products
    createProduct: async (payload) => {
        try {
            const response = await axios.post(
                ENDPOINTS.PRODUCTS.BASE,
                payload
            );
            return response.data;
        } catch (error) {
            console.error("Error creating product:", error);
            throw error;
        }
    },

    // PUT /api/products/{id}
    updateProduct: async (id, payload) => {
        try {
            const response = await axios.put(
                ENDPOINTS.PRODUCTS.DETAIL(id),
                payload
            );
            return response.data;
        } catch (error) {
            console.error("Error updating product:", error);
            throw error;
        }
    },

    // DELETE /api/products/{id}
    deleteProduct: async (id) => {
        try {
            const response = await axios.delete(
                ENDPOINTS.PRODUCTS.DETAIL(id)
            );
            return response.data;
        } catch (error) {
            console.error("Error deleting product:", error);
            throw error;
        }
    },

    reportProduct: async (id, payload) => {
        try {
            const response = await axios.post(
                ENDPOINTS.PRODUCTS.BASE + '/reports',
                payload
            );
            return response.data;
        } catch (error) {
            console.error("Error reporting product:", error);
            throw error;
        }
    },

    // ===============================
    // 2. Managed Products (Màn hình 5)
    // ===============================

    // GET /api/products/seller/{id}
    getSellerProducts: async (sellerId, params = { pageNumber: 1, pageSize: 10 }) => {
        try {
            const response = await axios.get(
                ENDPOINTS.PRODUCTS.SELLER(sellerId),
                { params }
            );
            return response.data;
        } catch (error) {
            console.error("Error fetching managed product list:", error);
            throw error;
        }
    },

    // GET /api/products/managed?page=1&tab=Reported
    getManagedProducts: async (params) => {
        try {
            const response = await axios.get(
                ENDPOINTS.PRODUCTS.MANAGED,
                { params } // query string
            );
            return response.data;
        } catch (error) {
            console.error("Error fetching administration list:", error);
            throw error;
        }
    },

    // GET /api/products/managed/{id}/violation-details
    getViolationDetails: async (id) => {
        try {
            const response = await axios.get(
                ENDPOINTS.PRODUCTS.VIOLATION_DETAILS(id)
            );
            return response.data;
        } catch (error) {
            console.error("Error fetching violation details:", error);
            throw error;
        }
    },

    // POST /api/products/managed/{id}/resolve-violation
    resolveViolation: async (id, payload) => {
        try {
            const response = await axios.post(
                ENDPOINTS.PRODUCTS.RESOLVE_VIOLATION(id),
                payload
            );
            return response.data;
        } catch (error) {
            console.error("Error resolving violation:", error);
            throw error;
        }
    }
};