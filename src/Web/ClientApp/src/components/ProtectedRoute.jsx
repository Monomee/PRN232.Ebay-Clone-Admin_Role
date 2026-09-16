import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext"; // ✅ import từ Context, không phải services/useAuth.js

export default function ProtectedRoute({ children, skip2FACheck = false, allowedRoles = [] }) {
  const { user, loading, isAuth } = useAuth();
  const navigate = useNavigate();

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
    if (allowedRoles.length > 0) {
      const userRole = user?.role;
      const hasRequiredRole = allowedRoles.includes(userRole);

      if (!hasRequiredRole) {
        console.warn(`Access denied for role: ${userRole}. Required: ${allowedRoles.join(", ")}`);
        navigate("/dashboard", { replace: true });
      }
    }
  }, [loading, isAuth, user, skip2FACheck, allowedRoles, navigate]);

  if (loading) return <LoadingScreen />;
  if (!isAuth) return null;
  if (!skip2FACheck && !user?.twoFactorEnabled) return null;

  // Final role check for rendering
  if (allowedRoles.length > 0 && !allowedRoles.includes(user?.role)) {
    return null;
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