import { useEffect } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { canAccess } from "../utils/access";
import { useAuth } from "../context/AuthContext"; // ✅ import từ Context, không phải services/useAuth.js

export default function ProtectedRoute({ children, skip2FACheck = false, allowedRoles = [], permission }) {
  const { user, loading, isAuth } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const allowed = canAccess(user, allowedRoles, permission);

  useEffect(() => {
    if (loading) return;

    if (!isAuth) {
      navigate("/login", { replace: true });
      return;
    }

    if (!skip2FACheck && !user?.twoFactorEnabled) {
      navigate("/enable2FA", { replace: true });
      return;
    }

    // Role-based authorization
    if (!allowed) {
      console.warn(`Access denied for role: ${user?.role}. Required: ${allowedRoles.join(", ")}`);
      // Sellers belong on their own page; admins fall back to the dashboard (not again if that is the denied page)
      if (user?.role === "Seller") navigate("/seller-products", { replace: true });
      else if (pathname !== "/dashboard" && pathname !== "/") navigate("/dashboard", { replace: true });
    }
  }, [loading, isAuth, user, skip2FACheck, allowed, allowedRoles, pathname, navigate]);

  if (loading) return <LoadingScreen />;
  if (!isAuth) return null;
  if (!skip2FACheck && !user?.twoFactorEnabled) return null;

  // Final role check for rendering
  if (!allowed) {
    return <p className="text-muted p-4">You do not have access to this page.</p>;
  }

  return children;
}

function LoadingScreen() {
  return (
    <div style={{
      minHeight: "100vh", display: "flex",
      alignItems: "center", justifyContent: "center",
      background: "linear-gradient(145deg, #eaf0fb, #f5f7fc)",
    }}>
      <div style={{
        width: 36, height: 36,
        border: "3px solid #dde3ef",
        borderTop: "3px solid #0064D2",
        borderRadius: "50%",
        animation: "spin 0.7s linear infinite",
      }} />
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}