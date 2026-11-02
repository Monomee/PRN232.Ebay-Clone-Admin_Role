import { apiRequest } from './httpClient';
import endpoints from './endpoints';

const orderService = {
  getOrders: (query) => {
    return apiRequest(endpoints.orders, { query });
  },

  // Cards over every order matching the same filter: { totalOrders, totalRevenue, activeOrders, completedOrders, successRate }
  getSummary: (query) => {
    return apiRequest(`${endpoints.orders}/summary`, { query });
  },

  getOrderById: (id) => {
    return apiRequest(`${endpoints.orders}/${id}`);
  }
};

export default orderService;
