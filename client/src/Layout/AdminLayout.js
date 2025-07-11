import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../Hooks/useAuth";

export default function AdminLayout({ children }) {
  const { user } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    } else if (user.role !== "Admin") {
      navigate("/"); // o mostrar página 403
    }
  }, [user]);

  if (!user || user.role !== "Admin") return null;

  return <>{children}</>;
}
