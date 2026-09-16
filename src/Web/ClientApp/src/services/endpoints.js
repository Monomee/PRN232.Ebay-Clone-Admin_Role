const API_BASE = "/api";

const endpoints = {
    users: `${API_BASE}/Users`,
    broadcasts: `${API_BASE}/Broadcasts`,
    adminRoles: `${API_BASE}/AdminRoles`,
    auditLogs: `${API_BASE}/AuditLogs`,
    wallets: `${API_BASE}/Wallets`,
    financials: `${API_BASE}/Financials`,
    categories: `${API_BASE}/Categories`,
    orders: `${API_BASE}/Orders`,
};

export default endpoints;

// src/constants/endpoints.js

export const ENDPOINTS = {
    ORDERS: {
        BASE: "/api/orders",
        DETAIL: (id) => `/api/orders/${id}`,
    },
    PRODUCTS: {
        BASE: "/api/products",
        MANAGED: "/api/products/managed",
        DETAIL: (id) => `/api/products/${id}`,
        // GET, POST danh sách chung
        BASE: '/api/products', 
        
        // GET danh sách quản lý (Màn hình 5)
        MANAGED: '/api/products/managed',
        
        // GET danh sách sản phẩm theo seller
        SELLER: (id) => `/api/products/seller/${id}`,
        
        // Cần truyền ID (PUT, DELETE, GET by ID) -> Dùng arrow function
        DETAIL: (id) => `/api/products/${id}`, 
        
        // Các tính năng đặc thù
        VIOLATION_DETAILS: (id) => `/api/products/managed/${id}/violation-details`,
        RESOLVE_VIOLATION: (id) => `/api/products/managed/${id}/resolve-violation`,
    },
    USERS: {
        BASE: "/api/users",
        DETAIL: (id) => `/api/users/${id}`,
        APPROVE: (id) => `/api/users/${id}/approve`,
        BAN: (id) => `/api/users/${id}/ban`,
        UNBAN: (id) => `/api/users/${id}/unban`,
    },


    // Sau này bạn có module khác thì cứ thêm vào đây:
    // USERS: { BASE: '/api/users', ... },
    // ORDERS: { ... }

    
    REVIEWS: {
        BASE: '/api/reviews',
        FLAGGED: '/api/reviews/flagged',
        STATUS: (id) => `/api/reviews/${id}/status`,
        REPLY: (id) => `/api/reviews/${id}/reply`,
        REPORT: (id) => `/api/reviews/${id}/report`,
    }
};
