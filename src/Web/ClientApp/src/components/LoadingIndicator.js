import React from "react";

export function LoadingIndicator({ text = "Loading..." }) {
  return (
    <div className="d-flex align-items-center gap-2 py-3" role="status" aria-live="polite">
      <div className="spinner-border spinner-border-sm text-primary" />
      <span>{text}</span>
    </div>
  );
}

