import { useEffect, useState } from "react";
import { useLocation, useNavigate, Outlet } from "react-router-dom";
import { Box, CircularProgress } from "@mui/material";

// ✅ rutas públicas que no necesitan login
const PUBLIC_ROUTES = [
  "/",
  "/login",
  "/privacy-policy",
  "/terms",
  "/dashboard",
  "/services",
  "/prices",
  "/contacts",
];

export default function ProtectedLayout() {
  const [loading, setLoading] = useState(true);
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    const session = localStorage.getItem("TOKEN_KEY");
    const isPublic = PUBLIC_ROUTES.includes(location.pathname);

    if (session || isPublic) {
      setLoading(false);
    } else {
      navigate("/login", { replace: true });
    }
  }, [location.pathname, navigate]);

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

  return <Outlet />;
}
