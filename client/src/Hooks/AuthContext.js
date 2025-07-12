import { createContext, useContext, useEffect, useState } from "react";

export const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const session = localStorage.getItem("session");
    return session ? JSON.parse(session) : null;
  });

  const [role, setRole] = useState(() => localStorage.getItem("role"));

  // Escucha cambios del localStorage (pestañas distintas o login)
  useEffect(() => {
    const session = localStorage.getItem("session");
    const storedRole = localStorage.getItem("role");

    setUser(session ? JSON.parse(session) : null);
    setRole(storedRole);
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
    <AuthContext.Provider value={{ user, role, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

// Hook para acceder al contexto
export const useAuth = () => {
  return useContext(AuthContext);
};
