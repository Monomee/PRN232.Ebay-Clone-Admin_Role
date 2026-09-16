import { apiRequest } from './httpClient';

const BASE = '/api/adminreturnrequests';

const returnRequestService = {
  // Màn hình 6.1 - Lấy danh sách nhưng cho phép lấy tất cả (truyền rỗng)
  getReturnRequests: (status = '') =>
    apiRequest(`${BASE}?status=${status}`),

  // Màn hình 6.1 - Bước 3: Lấy chi tiết 1 yêu cầu
  getReturnRequestDetail: (id) =>
    apiRequest(`${BASE}/${id}`),

  // Màn hình 6.1 - Bước 4: Chấp nhận hoàn tiền
  approveReturnRequest: (id, adminNote = '') =>
    apiRequest(`${BASE}/${id}/approve`, {
      method: 'POST',
      body: { returnRequestId: id, adminNote },
    }),

  // Màn hình 6.1 - Bước 4: Từ chối hoàn tiền
  rejectReturnRequest: (id, adminNote) =>
    apiRequest(`${BASE}/${id}/reject`, {
      method: 'POST',
      body: { returnRequestId: id, adminNote },
    }),

  // Phán quyết từ Admin (Adjudication)
  adjudicateReturnRequest: (id, data) =>
    apiRequest(`${BASE}/${id}/adjudicate`, {
      method: 'POST',
      body: {
        returnRequestId: id,
        adminNote: data.adminNote,
        resolutionAction: data.resolutionAction, // "RequireReturn", "KeepItem", "RefundWithoutReturn"
        isRefundedByEbayFund: data.isRefundedByEbayFund,
      },
    }),

  // Cấp mã vận đơn trả hàng (Return Facilitation)
  provideReturnLabel: (id, returnLabelUrl) =>
    apiRequest(`${BASE}/${id}/return-label`, {
      method: 'POST',
      body: { returnRequestId: id, returnLabelUrl },
    }),

  // Lấy tin nhắn làm bằng chứng (Evidence)
  getReturnRequestMessages: (id) =>
    apiRequest(`${BASE}/${id}/messages`),

  // Đóng băng yêu cầu (Fraud Prevention)
  freezeReturnRequest: (id, adminNote = '') =>
    apiRequest(`${BASE}/${id}/freeze`, {
      method: 'POST',
      body: { returnRequestId: id, adminNote },
    }),
};

export default returnRequestService;