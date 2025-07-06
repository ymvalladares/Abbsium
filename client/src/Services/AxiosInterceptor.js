// src/utils/axiosInterceptor.js
import axios from "axios";
import { snackbarRef } from "../Helpers/SnackbarUtils";

const axiosInstance = axios.create({
  baseURL: window.BaseUrlGeneral,
  headers: {
    "Content-Type": "application/json",
  },
});

// ➤ Interceptor de Request
axiosInstance.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("TOKEN_KEY");
    if (token) {
      config.headers["Authorization"] = `Bearer ${JSON.parse(token)}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ➤ Interceptor de Response
axiosInstance.interceptors.response.use(
  (response) => response,
  (error) => {
    const { enqueueSnackbar } = snackbarRef;
    const { response } = error;

    if (!response) {
      enqueueSnackbar("Network Error: Server is not running", {
        variant: "error",
      });
      return Promise.reject(error);
    }

    const { status, data } = response;

    const msg = typeof data === "object" && data.message ? data.message : null;

    // ❗ Dejar pasar errores controlados como "Invalid password", "Invalid email", etc.
    if (
      msg &&
      [
        "Invalid password !!!.",
        "Invalid email !!!.",
        "User not found.",
        "Account locked.",
        "Invalid login data.",
      ].includes(msg)
    ) {
      return Promise.reject(error); // El login lo manejará
    }

    // 🔔 Mostrar toast solo si no es un error esperado
    switch (status) {
      case 400:
        enqueueSnackbar(msg || "Check your URL (400)", {
          variant: "error",
        });
        break;
      case 401:
        enqueueSnackbar("Unahutorized (401)", { variant: "warning" });
        break;
      case 403:
        enqueueSnackbar("Access denied, Forbidden(403)", {
          variant: "info",
        });
        localStorage.removeItem("TOKEN_KEY");
        window.location.href = "/login";
        break;
      case 404:
        enqueueSnackbar("Not found (404)", { variant: "error" });
        break;
      case 500:
        enqueueSnackbar("Internal Server Error (500)", {
          variant: "error",
        });
        break;
      default:
        enqueueSnackbar("Unexpected error", { variant: "error" });
    }

    return Promise.reject(error);
  }
);

export default axiosInstance;
