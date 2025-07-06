// services/paymentService.js
import axiosInstance from "./AxiosInterceptor";

// 👉 Crear un nuevo pago (Stripe Checkout Session)
export const createPayment = async (amount, serviceType) => {
  const response = await axiosInstance.post(
    `${window.BaseUrlGeneral}Order/create-checkout-session`,
    {
      amount,
      serviceType,
    }
  );

  return response.data; // Retorna clientSecret
};

// 👉 Enviar datos genéricos (registro, login, etc.)
export const Pots_Request = async (url, newRecord) => {
  return axiosInstance.post(url, newRecord);
};
export const Get_Request = async (url) => axiosInstance.get(url);
export const Put_Request = async (url, data) => axiosInstance.put(url, data);
export const Delete_Request = async (url) => axiosInstance.delete(url);
