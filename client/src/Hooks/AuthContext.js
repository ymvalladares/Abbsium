import { createContext, useContext, useEffect, useState } from "react";

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

  const login = (userData, token, role) => {
    localStorage.setItem("session", JSON.stringify(userData));
    localStorage.setItem("TOKEN_KEY", token);
    localStorage.setItem("role", role);

    setUser(userData);
    setRole(role);
  };

  const logout = () => {
    localStorage.removeItem("session");
    localStorage.removeItem("TOKEN_KEY");
    localStorage.removeItem("role");

    setUser(null);
    setRole(null);
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
