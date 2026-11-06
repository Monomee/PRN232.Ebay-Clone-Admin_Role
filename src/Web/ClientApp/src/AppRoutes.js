import { DashboardPage } from "./pages/DashboardPage";
import { UsersPage } from "./pages/UsersPage";
import { UserDetailPage } from "./pages/UserDetailPage";
import { BroadcastPage } from "./pages/BroadcastPage";
import { AdminRolesPage } from "./pages/AdminRolesPage";
import { AuditLogsPage } from "./pages/AuditLogsPage";
import { SensitiveLogsPage } from "./pages/SensitiveLogsPage";
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
import RegisterPage from "./pages/RegisterPage";
import SellerBankAccountPage from "./pages/SellerBankAccountPage";

// Helper cho gọn
// permission: the API policy a custom admin role must grant to open the page
const protect = (element, allowedRoles = [], permission) => (
  <ProtectedRoute allowedRoles={allowedRoles} permission={permission}>
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
  {
    path: "/register",
    element: <RegisterPage />,
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
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"], "ViewDashboard")
  },
  {
    path: "/home",
    element: <EbayHomepage />,
    noLayout: true
  },
  {
    path: "/dashboard",
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"], "ViewDashboard")
  },
  {
    path: "/users",
    element: protect(<UsersPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },
  {
    path: "/users/:userId",
    element: protect(<UserDetailPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },
  {
    path: "/broadcasts",
    element: protect(<BroadcastPage />, ["SuperAdmin", "Support"], "ManageBroadcasts")
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
    path: "/sensitive-logs",
    element: protect(<SensitiveLogsPage />, ["SuperAdmin"])
  },
  {
    path: "/fetch-data",
    element: protect(<FetchData />, ["SuperAdmin"])
  },
  {
    path: "/disputes",
    element: protect(<DisputesPage />, ["SuperAdmin", "Support"], "ManageDisputes")
  },
  {
    path: "/disputes/dashboard",
    element: protect(<DisputeDashboard />, ["SuperAdmin", "Support"], "ManageDisputes")
  },
  {
    path: "/disputes/:id",
    element: protect(<DisputeDetailPage />, ["SuperAdmin", "Support"], "ManageDisputes")
  },


  {
    path: "/sellers",
    element: protect(<SellersPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },

  {
    path: '/products',
    element: protect(<ProductModerationPage/>, ["SuperAdmin", "Support"], "ManageProducts")
  },
  {
    path: '/review-monitoring',
    element: protect(<ReviewMonitoringPage />, ["SuperAdmin", "Support"], "ModerateReviews")
  },
  {
    path: "/public",
    element: protect(<ProductList />, ["SuperAdmin", "Support"])
  },
  {
    path: "/seller-products",
    element: protect(<SellerProductManagementPage />, ["SuperAdmin", "Support", "Seller"])
  },
  {
    path: "/seller-bank-account",
    element: protect(<SellerBankAccountPage />, ["Seller"])
  },
  {
    path: "/product/:id",
    element: <EbayProductDetail />,
    noLayout: true
  },

  {
    path: "/return-requests",
    element: protect(<ReturnRequestsPage />, ["SuperAdmin", "Support"], "ManageOrders")
  },
  {
    path: "/return-requests/:id",
    element: protect(<ReturnRequestDetailPage />, ["SuperAdmin", "Support"], "ManageOrders")
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
    element: protect(<ActiveConnectionsPage />, ["SuperAdmin"])
  },
  {
    path: "/orders",
    element: protect(<OrdersPage />, ["SuperAdmin", "Support", "Administrator"], "ManageOrders")
  }
];

export default AppRoutes;
