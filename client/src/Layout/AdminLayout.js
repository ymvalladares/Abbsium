import { useEffect } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../Hooks/useAuth";

export default function AdminLayout({ children }) {
  const { user, role } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    } else if (role !== "Admin") {
      navigate("/"); // o mostrar página 403
    }
  }, [user, role, navigate]);

  if (!user || role !== "Admin") return null;

  return (
    <>
      <Outlet />
    </>
  );
}
