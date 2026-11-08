import React, { useState } from 'react';
import { exportToExcel, exportToPDF } from '../../services/exportService';
import { notify } from '../../utils/toast';

export const DashboardActions = ({ metrics, stats, dashboardId, dateRange }) => {
  const [exporting, setExporting] = useState(false);

  const handleExcelExport = () => {
    const success = exportToExcel(metrics, stats, dateRange);
    if (!success) notify("Failed to export Excel.", 'error');
  };

  const handlePDFExport = async () => {
    setExporting(true);
    const success = await exportToPDF(dashboardId);
    setExporting(false);
    if (!success) notify("Failed to export PDF.", 'error');
  };

  return (
    <div className="d-flex gap-2 ms-2">
      <button
        type="button"
        className="btn btn-sm btn-light border d-inline-flex align-items-center gap-2 px-3"
        onClick={handleExcelExport}
        title="Download Excel spreadsheet"
      >
        <i className="bi bi-file-earmark-spreadsheet text-success" aria-hidden="true"></i>
        <span>Excel</span>
      </button>

      <button
        type="button"
        className={`btn btn-sm btn-light border d-inline-flex align-items-center gap-2 px-3 ${exporting ? 'disabled' : ''}`}
        onClick={handlePDFExport}
        title="Download PDF report"
        aria-busy={exporting}
      >
        {exporting ? (
          <span className="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        ) : (
          <i className="bi bi-file-earmark-pdf text-danger" aria-hidden="true"></i>
        )}
        <span>PDF</span>
      </button>
    </div>
  );
};
