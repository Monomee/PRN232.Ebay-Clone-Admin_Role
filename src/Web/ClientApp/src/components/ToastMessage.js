import React from "react";

export function ToastMessage({ message, type = "success", onClose }) {
  if (!message) {
    return null;
  }

  const alertClass = type === "error" ? "alert-danger" : "alert-success";

  return (
    <div className={`alert ${alertClass} d-flex justify-content-between align-items-center`} role="alert">
      <span>{message}</span>
      <button type="button" className="btn-close" aria-label="Close" onClick={onClose} />
    </div>
  );
}

