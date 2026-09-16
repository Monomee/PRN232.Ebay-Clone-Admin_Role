import React from 'react';
import { Link } from 'react-router-dom';
import { GlassCard } from './GlassCard';

const ActionItem = ({ title, count, icon, link, colorClass }) => (
  <Link to={link || "#"} className="action-center-item shadow-sm border-0">
    <div className={`icon-box rounded-circle mb-0 me-3 d-flex align-items-center justify-content-center bg-soft-${colorClass}`} style={{ width: 42, height: 42, flexShrink: 0 }}>
      <i className={`${icon} fs-5 text-${colorClass}`}></i>
    </div>
    <div className="flex-grow-1">
      <div className="fw-bold text-dark" style={{ fontSize: '0.9rem' }}>{title}</div>
      <div className="text-secondary small">Requires your attention</div>
    </div>
    <div className={`badge rounded-pill bg-soft-${colorClass} text-${colorClass} px-3 py-2 fw-bold`}>
      {count || 0}
    </div>
  </Link>
);

export const ActionCenter = ({ metrics }) => (
  <GlassCard className="p-4 h-100">
    <h6 className="fw-bold mb-3 d-flex align-items-center">
      <i className="bi bi-lightning-charge-fill text-warning me-2"></i>
      Action Center
    </h6>
    <div className="d-flex flex-column">
      <ActionItem
        title="User Approvals"
        count={metrics?.pendingAccountsCount}
        icon="bi bi-person-check"
        link="/users?tab=Pending"
        colorClass="blue"
      />
      <ActionItem
        title="Active Disputes"
        count={metrics?.openDisputesCount}
        icon="bi bi-shield-lock"
        link="/disputes?status=Open"
        colorClass="red"
      />
      <ActionItem
        title="Return Requests"
        count={metrics?.newReturnRequestsCount}
        icon="bi bi-arrow-left-right"
        link="/return-requests"
        colorClass="yellow"
      />
    </div>
  </GlassCard>
);
