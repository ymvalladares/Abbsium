import React, { useMemo } from "react";
import {
  Routes,
  Route,
  useNavigate,
  useLocation,
  Outlet,
} from "react-router-dom";
import { AppProvider } from "@toolpad/core";
import { Box, createTheme } from "@mui/material";
import AbbsiumLogo from "./Pictures/abbsium192.png";

// Icons
import DashboardIcon from "@mui/icons-material/Dashboard";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import PaymentsIcon from "@mui/icons-material/Payments";
import BrushIcon from "@mui/icons-material/Brush";
import ContactsIcon from "@mui/icons-material/Contacts";
import HomeRepairServiceIcon from "@mui/icons-material/HomeRepairService";
import AttachMoneyIcon from "@mui/icons-material/AttachMoney";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";

// Toolpad / UI
import { SnackbarProvider, useSnackbar } from "notistack";
import { setSnackbarRef } from "./Helpers/SnackbarUtils";
import { GoogleOAuthProvider } from "@react-oauth/google";

// Pages
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
import Layouts from "./Layout/Layouts";

// Auth
import { useAuth } from "./Hooks/useAuth";
import AdminSite from "./Pages/AdminSite";

const demoTheme = createTheme({
  cssVariables: {
    colorSchemeSelector: "data-toolpad-color-scheme",
  },
  colorSchemes: { light: true, dark: false },
});

function AppWithSnackbar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, role, logout } = useAuth();

  const snackbar = useSnackbar();
  setSnackbarRef(snackbar);

  const NAVIGATION = useMemo(() => {
    const baseNav = [
      { kind: "header", title: "Web Sites" },
      { segment: "dashboard", title: "Dashboard", icon: <DashboardIcon /> },
      {
        segment: "services",
        title: "Services",
        icon: <HomeRepairServiceIcon />,
      },
      { segment: "prices", title: "Prices", icon: <PaymentsIcon /> },
      { segment: "contacts", title: "Contact", icon: <ContactsIcon /> },
      { kind: "divider" },
      { kind: "header", title: "Subscriptions" },
      { segment: "orders", title: "Orders", icon: <ShoppingCartIcon /> },
      {
        segment: "carpentry-design",
        title: "Carpentry Design",
        icon: <BrushIcon />,
      },
      { kind: "divider" },
      { kind: "header", title: "Earn Money" },
      {
        segment: "investments",
        title: "Investments",
        icon: <AttachMoneyIcon />,
      },
      {
        segment: "admin-layout",
        title: "Admin Layout",
        icon: <AttachMoneyIcon />,
      },
      {
        segment: "users",
        title: "Users",
        icon: <AttachMoneyIcon />,
      },
    ];

    if (role === "Admin") {
      baseNav.push({ kind: "divider" });
      baseNav.push({ kind: "header", title: "Admin" });
      baseNav.push({
        segment: "users",
        title: "Users",
        icon: <AdminPanelSettingsIcon />,
      });
    }

    return baseNav;
  }, [role]);

  const authentication = useMemo(
    () => ({
      signIn: () => {
        navigate("/login");
      },
      signOut: () => {
        logout();
        navigate("/dashboard");
      },
    }),
    [logout, navigate]
  );

  const router = {
    navigate: (path) => navigate(path),
    pathname: location.pathname,
  };

  const session = { user };

  return (
    <GoogleOAuthProvider clientId="957373776882-nvru55mvgqctlt1o7viqo0iisrrif4k5.apps.googleusercontent.com">
      <AppProvider
        session={session}
        navigation={NAVIGATION}
        authentication={authentication}
        router={router}
        theme={demoTheme}
        branding={{
          logo: (
            <Box
              component="img"
              src={AbbsiumLogo}
              alt="Abbsium Logo"
              height={25}
            />
          ),
          title: "Abbsium",
        }}
      >
        <Routes>
          <Route path="/" element={<Layouts />}>
            <Route index element={<Dashboard />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="services" element={<Services />} />
            <Route path="prices" element={<Prices />} />
            <Route path="contacts" element={<Contacts />} />
            <Route path="investments" element={<Games />} />
            <Route path="privacy-policy" element={<PrivacyPolicy />} />
            <Route path="terms" element={<TermsOfUse />} />
            <Route path="login" element={<Login />} />

            <Route element={<ProtectedLayout />}>
              <Route path="orders" element={<Orders />} />
              <Route path="carpentry-design" element={<CarpentryDesign />} />
              <Route path="success-payment" element={<Success_payment />} />
              <Route path="payment-denied" element={<Failure_payment />} />
            </Route>

            {/* <Route element={<AdminLayout />}>
              <Route path="users" element={<Users />} />
            </Route> */}
            <Route path="users" element={<Users />} />

            <Route path="admin-layout" element={<AdminSite />} />

            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
      </AppProvider>
    </GoogleOAuthProvider>
  );
}

export default function App() {
  return (
    <SnackbarProvider
      maxSnack={3}
      anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      autoHideDuration={3000}
    >
      <AppWithSnackbar />
    </SnackbarProvider>
  );
}
