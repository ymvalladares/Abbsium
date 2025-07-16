import { useLocation, Navigate, Outlet } from "react-router-dom";
import { Box, CircularProgress } from "@mui/material";
import { useAuth } from "../Hooks/useAuth"; // ajusta el path si es diferente

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
  const { user, isAuthLoaded } = useAuth();
  const location = useLocation();

  // Esperar a que se cargue el auth context
  if (!isAuthLoaded) {
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

  // Si es una ruta pública, permitir siempre
  if (PUBLIC_ROUTES.includes(location.pathname)) {
    return <Outlet />;
  }

  // Si no hay sesión, redirigir al login
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Si está autenticado, permitir acceso
  return <Outlet />;
}
