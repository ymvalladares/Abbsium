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
import { useEffect } from "react";
import { SnackbarProvider, useSnackbar } from "notistack";
import { setSnackbarRef } from "./Helpers/SnackbarUtils";
import { GoogleOAuthProvider } from "@react-oauth/google";

const NAVIGATION = [
  { kind: "header", title: "Web Sites" },
  { segment: "dashboard", title: "Dashboard", icon: <DashboardIcon /> },
  { segment: "services", title: "Services", icon: <HomeRepairServiceIcon /> },
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
  { segment: "investments", title: "Investments", icon: <AttachMoneyIcon /> },
];

const demoTheme = createTheme({
  cssVariables: {
    colorSchemeSelector: "data-toolpad-color-scheme",
  },
  colorSchemes: { light: true, dark: false },
});

export default function App() {
  const navigate = useNavigate();
  const location = useLocation();

  const [session, setSession] = useState(() => {
    const stored = localStorage.getItem("session");
    return stored ? JSON.parse(stored) : null;
  });

  const authentication = useMemo(
    () => ({
      signIn: () => {
        navigate("/login");
      },
      signOut: () => {
        localStorage.removeItem("session");
        localStorage.removeItem("TOKEN_KEY");
        setSession(null);
        navigate("/dashboard");
      },
    }),
    [navigate]
  );

  const router = {
    navigate: (path) => navigate(path),
    pathname: location.pathname,
  };

  function AppWithSnackbar() {
    const snackbar = useSnackbar();
    setSnackbarRef(snackbar);
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
