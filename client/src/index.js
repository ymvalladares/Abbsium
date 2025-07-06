import React from "react";
import App from "./App";
import ReactDOM from "react-dom/client";
import "./index.css";
import reportWebVitals from "./reportWebVitals";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import Layouts from "./Layout/Layouts";
import Dashboard from "./Pages/Dashboard";
import Orders from "./Pages/Orders";
import CarpentryDesign from "./Pages/CarpentryDesign";
import Contacts from "./Pages/Contacts";
import Prices from "./Pages/Prices";
import Services from "./Pages/Services";
import PrivacyPolicy from "./Pages/PrivacyPolicy";
import TermsOfUse from "./Pages/TermsOfUse";
import Games from "./Pages/Games";
import Login from "./Login/Login";
import NotFound from "./Pages/NotFound";
import ProtectedLayout from "./Helpers/ProtectedLayout";
import Success_payment from "./Pages/Success_payment";
import Failure_payment from "./Pages/Failure_payment";

const router = createBrowserRouter([
  {
    Component: App,
    children: [
      {
        path: "/",
        Component: Layouts,
        children: [
          { path: "/", Component: Dashboard },
          { path: "/dashboard", Component: Dashboard },
          { path: "/services", Component: Services },
          { path: "/prices", Component: Prices },
          { path: "/contacts", Component: Contacts },
          { path: "/investments", Component: Games },
          { path: "/privacy-policy", Component: PrivacyPolicy },
          { path: "/terms", Component: TermsOfUse },
          { path: "/login", Component: Login },
          { path: "/success-payment", Component: Success_payment },
          { path: "/payment-denied", Component: Failure_payment },

          // Protected Routes go under ProtectedLayout
          {
            Component: ProtectedLayout,
            children: [
              { path: "/orders", Component: Orders },
              { path: "/carpentry-design", Component: CarpentryDesign },
            ],
          },

          { path: "*", Component: NotFound },
        ],
      },
    ],
  },
]);

window.BaseUrl = "https://localhost:44328/";
//window.BaseUrlGeneral = "https://192.168.86.25:45455/";
window.BaseUrlGeneral = "https://abbsium.onrender.com/";

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
