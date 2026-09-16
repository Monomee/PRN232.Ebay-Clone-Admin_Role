import { apiRequest } from './httpClient';
import endpoints from './endpoints';

const orderService = {
  getOrders: (query) => {
    return apiRequest(endpoints.orders, { query });
  },

  getOrderById: (id) => {
    return apiRequest(`${endpoints.orders}/${id}`);
  }
};

export default orderService;
