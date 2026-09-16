import { DashboardPage } from "./pages/DashboardPage";
import { UsersPage } from "./pages/UsersPage";
import { UserDetailPage } from "./pages/UserDetailPage";
import { BroadcastPage } from "./pages/BroadcastPage";
import { AdminRolesPage } from "./pages/AdminRolesPage";
import { AuditLogsPage } from "./pages/AuditLogsPage";
import { FetchData } from "./components/FetchData";
import { DisputesPage } from "./pages/DisputesPage";
import { DisputeDashboard } from "./pages/DisputeDashboard";
import { DisputeDetailPage } from "./pages/DisputeDetailPage";
import LoginPage from "./pages/LoginPage";
import Enable2FAPage from "./pages/Enable2fapage";
import ProtectedRoute from "./components/ProtectedRoute";
import EbayHomepage from "./pages/EbayHomepage";
import ReturnRequestsPage from './pages/ReturnRequestsPage';
import ReturnRequestDetailPage from "./pages/ReturnRequestDetailPage";
import { SellersPage } from "./pages/SellersPage";

import { ReviewMonitoringPage } from "./pages/ReviewMonitoringPage";
import ProductModerationPage from "./pages/ProductModerationPage";
import { ProductList } from "./pages/Public/Home";
import EbayProductDetail from "./pages/EbayProductDetail";




import SellerProductManagementPage from "./pages/SellerProductManagementPage";
import { MockPage } from "./pages/MockPage";
import { SellerPendingFundsPage } from "./pages/SellerPendingFundsPage";
import PayoutEnginePage from "./pages/PayoutEnginePage";
import ActiveConnectionsPage from "./pages/ActiveConnectionsPage.jsx";
import OrdersPage from "./pages/OrdersPage.jsx";

// Helper cho gọn
const protect = (element, allowedRoles = []) => (
  <ProtectedRoute allowedRoles={allowedRoles}>
    {element}
  </ProtectedRoute>
);

const AppRoutes = [
  // ── Public ───────────────────────────────────────────────────────
  {
    path: "/login",
    element: <LoginPage />,
    noLayout: true
  },
  // ── Cần login nhưng chưa cần 2FA ─────────────────────────────────
  {
    path: "/enable2FA",
    element: (
      <ProtectedRoute skip2FACheck>
        <Enable2FAPage />
      </ProtectedRoute>
    ),
    noLayout: true
  },

  // ── Cần login + bắt buộc có 2FA ──────────────────────────────────
  {
    index: true,
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"])
  },
  {
    path: "/home",
    element: <EbayHomepage />,
    noLayout: true
  },
  {
    path: "/dashboard",
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"])
  },
  {
    path: "/users",
    element: protect(<UsersPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/users/:userId",
    element: protect(<UserDetailPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/broadcasts",
    element: protect(<BroadcastPage />, ["SuperAdmin"])
  },
  {
    path: "/admin-roles",
    element: protect(<AdminRolesPage />, ["SuperAdmin"])
  },
  {
    path: "/audit-logs",
    element: protect(<AuditLogsPage />, ["SuperAdmin"])
  },
  {
    path: "/fetch-data",
    element: protect(<FetchData />, ["SuperAdmin"])
  },
  {
    path: "/disputes",
    element: protect(<DisputesPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/disputes/dashboard",
    element: protect(<DisputeDashboard />, ["SuperAdmin", "Support"])
  },
  {
    path: "/disputes/:id",
    element: protect(<DisputeDetailPage />, ["SuperAdmin", "Support"])
  },


  {
    path: "/sellers",
    element: protect(<SellersPage />, ["SuperAdmin"])
  },

  {
    path: '/products',
    element: protect(<ProductModerationPage/>, ["SuperAdmin", "Support"])
  },
  {
    path: '/review-monitoring',
    element: protect(<ReviewMonitoringPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/public",
    element: protect(<ProductList />, ["SuperAdmin", "Support"])
  },
  {
    path: "/seller-products",
    element: protect(<SellerProductManagementPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/product/:id",
    element: <EbayProductDetail />,
    noLayout: true
  },

  {
    path: "/return-requests",
    element: protect(<ReturnRequestsPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/return-requests/:id",
    element: protect(<ReturnRequestDetailPage />, ["SuperAdmin", "Support"])
  },
  {
    path: "/mock",
    element: protect(<MockPage />, ["SuperAdmin"])
  },
  {
    path: "/sellers/pending/:sellerId",
    element: protect(<SellerPendingFundsPage />, ["SuperAdmin"])
  },
  {
    path: "/payout-engine",
    element: protect(<PayoutEnginePage />, ["SuperAdmin"])
  },
  {
    path: "/active-connections",
    element: protect(<ActiveConnectionsPage />)
  },
  {
    path: "/orders",
    element: protect(<OrdersPage />)
  }
];

export default AppRoutes;
