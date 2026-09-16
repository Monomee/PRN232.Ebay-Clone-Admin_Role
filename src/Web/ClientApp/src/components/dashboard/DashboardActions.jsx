import React, { useState } from 'react';
import { exportToExcel, exportToPDF } from '../../services/exportService';

export const DashboardActions = ({ metrics, stats, dashboardId, dateRange }) => {
  const [exporting, setExporting] = useState(false);

  const handleExcelExport = () => {
    const success = exportToExcel(metrics, stats, dateRange);
    if (!success) alert("Failed to export Excel.");
  };

  const handlePDFExport = async () => {
    setExporting(true);
    const success = await exportToPDF(dashboardId);
    setExporting(false);
    if (!success) alert("Failed to export PDF.");
  };

  return (
    <div className="d-flex gap-2 ms-2">
      <button 
        className="btn btn-sm rounded-circle d-flex align-items-center justify-content-center border-0 shadow-sm"
        onClick={handleExcelExport}
        title="Download Excel Spreadsheet"
        style={{ 
          width: 36, 
          height: 36, 
          backgroundColor: '#15833b', 
          color: '#fff',
          transition: 'all 0.2s',
        }}
        onMouseOver={(e) => e.currentTarget.style.transform = 'scale(1.1)'}
        onMouseOut={(e) => e.currentTarget.style.transform = 'scale(1)'}
      >
        <div className="d-flex flex-column align-items-center" style={{ lineHeight: 0.8 }}>
          <i className="bi bi-file-earmark-arrow-down-fill" style={{ fontSize: '16px' }}></i>
          <span style={{ fontSize: '7px', fontWeight: '900', marginTop: '2px' }}>XLS</span>
        </div>
      </button>
      
      <button 
        className={`btn btn-sm rounded-circle d-flex align-items-center justify-content-center border-0 shadow-sm ${exporting ? 'disabled' : ''}`}
        onClick={handlePDFExport}
        title="Download PDF Report"
        style={{ 
          width: 36, 
          height: 36, 
          backgroundColor: '#e0103a', 
          color: '#fff',
          transition: 'all 0.2s',
        }}
        onMouseOver={(e) => e.currentTarget.style.transform = 'scale(1.1)'}
        onMouseOut={(e) => e.currentTarget.style.transform = 'scale(1)'}
      >
        {exporting ? (
          <span className="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        ) : (
          <div className="d-flex flex-column align-items-center" style={{ lineHeight: 0.8 }}>
            <i className="bi bi-file-earmark-arrow-down-fill" style={{ fontSize: '16px' }}></i>
            <span style={{ fontSize: '7px', fontWeight: '900', marginTop: '2px' }}>PDF</span>
          </div>
        )}
      </button>
    </div>
  );
};
