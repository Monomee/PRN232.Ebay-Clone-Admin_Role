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
    path: "/login", title: "Sign in",
    element: <LoginPage />,
    noLayout: true
  },
  {
    path: "/register", title: "Create account",
    element: <RegisterPage />,
    noLayout: true
  },
  // ── Cần login nhưng chưa cần 2FA ─────────────────────────────────
  {
    path: "/enable2FA", title: "Two-factor setup",
    element: (
      <ProtectedRoute skip2FACheck>
        <Enable2FAPage />
      </ProtectedRoute>
    ),
    noLayout: true
  },

  // ── Cần login + bắt buộc có 2FA ──────────────────────────────────
  {
    index: true, title: "Dashboard",
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"], "ViewDashboard")
  },
  {
    path: "/home", title: "eBay: Electronics, Fashion, Collectibles & more",
    element: <EbayHomepage />,
    noLayout: true
  },
  {
    path: "/dashboard", title: "Dashboard",
    element: protect(<DashboardPage />, ["SuperAdmin", "Support", "Monitor"], "ViewDashboard")
  },
  {
    path: "/users", title: "Users",
    element: protect(<UsersPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },
  {
    path: "/users/:userId", title: "User details",
    element: protect(<UserDetailPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },
  {
    path: "/broadcasts", title: "Broadcasts",
    element: protect(<BroadcastPage />, ["SuperAdmin", "Support"], "ManageBroadcasts")
  },
  {
    path: "/admin-roles", title: "Admin roles",
    element: protect(<AdminRolesPage />, ["SuperAdmin"])
  },
  {
    path: "/audit-logs", title: "Audit logs",
    element: protect(<AuditLogsPage />, ["SuperAdmin"])
  },
  {
    path: "/sensitive-logs", title: "Sensitive logs",
    element: protect(<SensitiveLogsPage />, ["SuperAdmin"])
  },
  {
    path: "/fetch-data", title: "Fetch data",
    element: protect(<FetchData />, ["SuperAdmin"])
  },
  {
    path: "/disputes", title: "Disputes",
    element: protect(<DisputesPage />, ["SuperAdmin", "Support"], "ManageDisputes")
  },
  {
    path: "/disputes/dashboard", title: "Dispute analytics",
    element: protect(<DisputeDashboard />, ["SuperAdmin", "Support"], "ManageDisputes")
  },
  {
    path: "/disputes/:id", title: "Dispute details",
    element: protect(<DisputeDetailPage />, ["SuperAdmin", "Support"], "ManageDisputes")
  },


  {
    path: "/sellers", title: "Sellers",
    element: protect(<SellersPage />, ["SuperAdmin", "Support"], "ManageUsers")
  },

  {
    path: '/products', title: "Product moderation",
    element: protect(<ProductModerationPage/>, ["SuperAdmin", "Support"], "ManageProducts")
  },
  {
    path: '/review-monitoring', title: "Reviews",
    element: protect(<ReviewMonitoringPage />, ["SuperAdmin", "Support"], "ModerateReviews")
  },
  {
    path: "/public", title: "Public listings",
    element: protect(<ProductList />, ["SuperAdmin", "Support"])
  },
  {
    path: "/seller-products", title: "Your products",
    element: protect(<SellerProductManagementPage />, ["SuperAdmin", "Support", "Seller"])
  },
  {
    path: "/seller-bank-account", title: "Payout bank account",
    element: protect(<SellerBankAccountPage />, ["Seller"])
  },
  {
    path: "/product/:id", title: "Product · eBay",
    element: <EbayProductDetail />,
    noLayout: true
  },

  {
    path: "/return-requests", title: "Return requests",
    element: protect(<ReturnRequestsPage />, ["SuperAdmin", "Support"], "ManageOrders")
  },
  {
    path: "/return-requests/:id", title: "Return request",
    element: protect(<ReturnRequestDetailPage />, ["SuperAdmin", "Support"], "ManageOrders")
  },
  {
    path: "/mock", title: "Simulation toolbox",
    element: protect(<MockPage />, ["SuperAdmin"])
  },
  {
    path: "/sellers/pending/:sellerId", title: "Seller pending funds",
    element: protect(<SellerPendingFundsPage />, ["SuperAdmin"])
  },
  {
    path: "/payout-engine", title: "Payout engine",
    element: protect(<PayoutEnginePage />, ["SuperAdmin"])
  },
  {
    path: "/active-connections", title: "Active connections",
    element: protect(<ActiveConnectionsPage />, ["SuperAdmin"])
  },
  {
    path: "/orders", title: "Orders",
    element: protect(<OrdersPage />, ["SuperAdmin", "Support", "Administrator"], "ManageOrders")
  }
];

export default AppRoutes;
