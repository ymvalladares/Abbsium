import { createContext, useContext, useEffect, useState } from "react";

export const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const session = localStorage.getItem("session");
    return session ? JSON.parse(session) : null;
  });

  // Escucha cambios del localStorage entre pestañas
  useEffect(() => {
    const handleStorageChange = () => {
      const session = localStorage.getItem("session");
      setUser(session ? JSON.parse(session) : null);
    };

    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("session-updated", handleStorageChange);

    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("session-updated", handleStorageChange);
    };
  }, []);

  const login = (userData) => {
    localStorage.setItem("session", JSON.stringify(userData));
    localStorage.setItem("TOKEN_KEY", userData.token);
    setUser(userData);
    window.dispatchEvent(new Event("session-updated")); // para otras pestañas
  };

  const logout = () => {
    localStorage.removeItem("session");
    localStorage.removeItem("TOKEN_KEY");
    setUser(null);
    window.dispatchEvent(new Event("session-updated"));
  };

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
