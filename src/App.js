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
        setSession(null);
      },
    }),
    [navigate]
  );

  const router = {
    navigate: (path) => navigate(path),
    pathname: location.pathname,
  };

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
