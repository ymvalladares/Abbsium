import { createContext, useContext, useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Pots_Request } from "../Services/PaymentService";

export const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [role, setRole] = useState(null);
  const [isAuthLoaded, setIsAuthLoaded] = useState(false);

  useEffect(() => {
    const session = localStorage.getItem("session");
    const storedRole = localStorage.getItem("role");

    if (!session) {
      setIsAuthLoaded(true);
      return;
    }

    const parsedUser = JSON.parse(session);

    // Precargar imagen si existe
    if (parsedUser.image) {
      const img = new Image();
      img.src = parsedUser.image;
      img.onload = () => {
        setUser(parsedUser);
        setRole(storedRole);
        setIsAuthLoaded(true);
      };
    } else {
      setUser(parsedUser);
      setRole(storedRole);
      setIsAuthLoaded(true);
    }
  }, []);

  const login = (userData, accessToken, role, refreshToken) => {
    localStorage.setItem("session", JSON.stringify(userData));
    localStorage.setItem("TOKEN_KEY", accessToken);
    localStorage.setItem("REFRESH_KEY", refreshToken);
    localStorage.setItem("role", role);

    setUser(userData);
    setRole(role);
  };

  const logout = async () => {
    const refreshToken = localStorage.getItem("REFRESH_KEY");
    const tokenKey = localStorage.getItem("TOKEN_KEY");

    // Limpiar localStorage y contexto
    setUser(null);
    setRole(null);
    localStorage.removeItem("session");
    localStorage.removeItem("TOKEN_KEY");
    localStorage.removeItem("REFRESH_KEY");
    localStorage.removeItem("role");

    if (!refreshToken) return;

    const tokens = {
      accessToken: tokenKey,
      refreshToken: refreshToken,
    };

    try {
      await Pots_Request(`${window.BaseUrl}Account/logout`, tokens);
    } catch (error) {
      console.error("Logout failed:", error);
    }
  };

  return (
    <AuthContext.Provider value={{ user, role, login, logout, isAuthLoaded }}>
      {children}
    </AuthContext.Provider>
  );
}

// Hook para acceder al contexto
export const useAuth = () => {
  return useContext(AuthContext);
};
