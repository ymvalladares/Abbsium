// index.js o main.jsx
import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import { HashRouter, Routes, Route } from "react-router-dom";

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
import AdminLayout from "./Layout/AdminLayout";
import Users from "./Pages/Users";
import { AuthProvider } from "./Hooks/AuthContext";

window.BaseUrl = "https://localhost:44328/";
// window.BaseUrlGeneral = "https://192.168.86.25:45455/";
window.BaseUrlGeneral = "https://abbsium.onrender.com/";

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(
  <React.StrictMode>
    <HashRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </HashRouter>
  </React.StrictMode>
);

reportWebVitals();
