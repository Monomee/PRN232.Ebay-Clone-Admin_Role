import axios from 'axios';

export const dashboardService = {
  getMetrics: async (startDate, endDate) => {
    const response = await axios.get('/api/dashboard/metrics', {
      params: { startDate, endDate }
    });
    return response.data;
  }
};
