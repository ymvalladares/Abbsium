// src/utils/axiosInterceptor.js
import axios from "axios";
import { snackbarRef } from "../Helpers/SnackbarUtils";

const axiosInstance = axios.create({
  baseURL: window.BaseUrl,
  headers: {
    "Content-Type": "application/json",
  },
});

// ===================== REQUEST =====================
axiosInstance.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("TOKEN_KEY");
    if (token) {
      config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ===================== RESPONSE =====================
axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { enqueueSnackbar } = snackbarRef;
    const originalRequest = error.config;

    // No response = server down
    if (!error.response) {
      enqueueSnackbar("Network Error: Server is not running", {
        variant: "error",
      });
      return Promise.reject(error);
    }

    const { status, data } = error.response;
    const message =
      typeof data === "object" && data.message ? data.message : null;

    // ⛔ Evitar ciclo infinito
    if (originalRequest._retry) {
      return Promise.reject(error);
    }

    // ===================== 401 → REFRESH TOKEN =====================
    if (status === 401) {
      const refreshToken = localStorage.getItem("REFRESH_KEY");
      const accessToken = localStorage.getItem("TOKEN_KEY");

      if (!refreshToken) return Promise.reject(error);

      try {
        originalRequest._retry = true;

        const res = await axios.post(`${window.BaseUrl}Account/refresh-token`, {
          accessToken,
          refreshToken,
        });

        const newAccessToken = res.data.token;
        const newRefreshToken = res.data.refreshToken;

        localStorage.setItem("TOKEN_KEY", newAccessToken);
        localStorage.setItem("REFRESH_KEY", newRefreshToken);

        // Reintentar la request original
        originalRequest.headers["Authorization"] = `Bearer ${newAccessToken}`;

        return axiosInstance(originalRequest);
      } catch (refreshError) {
        // Refresh falló → volver a login
        localStorage.removeItem("TOKEN_KEY");
        localStorage.removeItem("REFRESH_KEY");

        window.location.href = "/login";
        return Promise.reject(refreshError);
      }
    }

    // ===================== OTHER ERRORS =====================
    switch (status) {
      case 400:
        enqueueSnackbar(message || "Bad request (400)", { variant: "error" });
        break;
      case 403:
        enqueueSnackbar("Access denied (403)", { variant: "info" });
        break;
      case 404:
        enqueueSnackbar("Not found (404)", { variant: "error" });
        break;
      case 500:
        enqueueSnackbar("Internal Server Error (500)", { variant: "error" });
        break;
      default:
        enqueueSnackbar("Unexpected error", { variant: "error" });
        break;
    }

    return Promise.reject(error);
  }
);

export default axiosInstance;
