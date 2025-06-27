import React, { useEffect, useMemo, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { AppProvider } from "@toolpad/core";
import { Box, createTheme, CircularProgress } from "@mui/material";
import AbbsiumLogo from "./Pictures/abbsium192.png";

// Icons
import DashboardIcon from "@mui/icons-material/Dashboard";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import PaymentsIcon from "@mui/icons-material/Payments";
import BrushIcon from "@mui/icons-material/Brush";
import ContactsIcon from "@mui/icons-material/Contacts";
import HomeRepairServiceIcon from "@mui/icons-material/HomeRepairService";
import AttachMoneyIcon from "@mui/icons-material/AttachMoney";

// ✅ rutas públicas que no necesitan login
const PUBLIC_ROUTES = [
  "/login",
  "/privacy-policy",
  "/terms",
  "/dashboard",
  "/services",
  "/prices",
  "/contacts",
];

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

  const [session, setSession] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkAccess = async () => {
      const params = new URLSearchParams(window.location.search);
      const redirect = params.get("redirect");

      if (redirect && location.pathname === "/") {
        navigate(redirect, { replace: true });
        return;
      }

      const sessionStored = localStorage.getItem("session");
      const isPublic = PUBLIC_ROUTES.includes(location.pathname);

      if (sessionStored) {
        setSession(JSON.parse(sessionStored));
        setLoading(false);
      } else if (isPublic) {
        setLoading(false);
      } else {
        navigate("/login", { replace: true });
        // ❌ importante: NO desactives loading aquí para evitar flicker
      }
    };

    checkAccess();
  }, [location.pathname, navigate]);

  const authentication = React.useMemo(() => {
    return {
      signIn: () => {
        navigate("/login");
      },
      signOut: () => {
        localStorage.removeItem("session");
        setSession(null);
      },
    };
  }, [navigate]);

  const router = {
    navigate: (path) => navigate(path),
    pathname: location.pathname,
  };

  // ⏳ Evita mostrar la app hasta confirmar acceso
  if (loading) {
    return (
      <Box
        minHeight="100vh"
        display="flex"
        justifyContent="center"
        alignItems="center"
      >
        <CircularProgress />
      </Box>
    );
  }

  return (
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
  );
}
