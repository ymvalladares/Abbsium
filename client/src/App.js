import React, { useMemo, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
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
import AccountCircleIcon from "@mui/icons-material/AccountCircle";

// Toolpad / UI
import { SnackbarProvider, useSnackbar } from "notistack";
import { setSnackbarRef } from "./Helpers/SnackbarUtils";
import { GoogleOAuthProvider } from "@react-oauth/google";

// Auth
import { useAuth } from "./Hooks/useAuth";

const demoTheme = createTheme({
  cssVariables: {
    colorSchemeSelector: "data-toolpad-color-scheme",
  },
  colorSchemes: { light: true, dark: false },
});

export default function App() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, role, logout } = useAuth();

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

  function AppWithSnackbar() {
    const snackbar = useSnackbar();
    setSnackbarRef(snackbar);

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
          <Outlet />
        </AppProvider>
      </GoogleOAuthProvider>
    );
  }

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
