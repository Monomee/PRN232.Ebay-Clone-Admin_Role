import axios from "axios";
import { guardAxios } from "./sessionGuard";

const instance = axios.create({
    baseURL: "/",
    // timeout: 1000,
});

guardAxios(instance);

instance.interceptors.request.use(
    function (config) {
        

        return config;
    },
    function (error) {
        return Promise.reject(error);
    }
);

instance.interceptors.response.use(
    function (response) {

        return response;
    },
    function (error) {
        return Promise.reject(error);
    }
);

export default instance;