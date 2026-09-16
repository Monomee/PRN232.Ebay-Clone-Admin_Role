import axios from "axios";
import endpoints, { ENDPOINTS } from "./endpoints";

export const categoryService = {
     getAllCategories: async () => {
        try {
            const response = await axios.get(endpoints.categories);
            return response.data;
        } catch (error) {
            console.error("Lỗi khi lấy danh sách danh mục:", error);
            throw error;
        }
    },
}