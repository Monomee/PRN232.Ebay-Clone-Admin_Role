import * as XLSX from 'xlsx';
import html2canvas from 'html2canvas';
import { jsPDF } from 'jspdf';

/**
 * Exports dashboard data to an Excel file with multiple sheets.
 */
export const exportToExcel = (metrics, stats, dateRange, fileName = 'Dashboard_Report') => {
  try {
    const wb = XLSX.utils.book_new();

    // Combine all data into one array for a single sheet
    const combinedData = [
      ['DASHBOARD REPORT'],
      ['Report Period', dateRange || 'All Time'],
      ['Generated On', new Date().toLocaleString()],
      [''],
      ['SECTION: OVERVIEW'],
      ['Metric', 'Value'],
      ['Total Users', metrics?.totalUsers || 0],
      ['Total Products', metrics?.totalProducts || 0],
      ['Orders (Selected Range)', metrics?.totalOrdersToday || 0],
      ['Pending Accounts', metrics?.pendingAccountsCount || 0],
      ['Open Disputes', metrics?.openDisputesCount || 0],
      ['New Return Requests', metrics?.newReturnRequestsCount || 0],
      [''],
      ['SECTION: ORDER DISTRIBUTION'],
      ['Status', 'Count'],
      ['Completed', stats.orders?.completed || 0],
      ['Delivered', stats.orders?.delivered || 0],
      ['Returned', stats.orders?.returned || 0],
      ['Total', stats.orders?.total || 0],
      [''],
      ['SECTION: REVENUE BREAKDOWN'],
      ['Date', 'Revenue Amount']
    ];

    if (stats.revenue?.dailyRevenue) {
      stats.revenue.dailyRevenue.forEach(d => {
        combinedData.push([new Date(d.date).toLocaleDateString(), d.amount || d.revenue || 0]);
      });
      combinedData.push([''], ['TOTAL REVENUE', stats.revenue.totalRevenue || 0]);
    }

    const ws = XLSX.utils.aoa_to_sheet(combinedData);
    
    // Simple column width styling
    ws['!cols'] = [{ wch: 30 }, { wch: 20 }];

    XLSX.utils.book_append_sheet(wb, ws, 'Dashboard Report');

    XLSX.writeFile(wb, `${fileName}_${new Date().toISOString().split('T')[0]}.xlsx`);
    return true;
  } catch (error) {
    console.error('Excel Export Error:', error);
    return false;
  }
};

/**
 * Captures a DOM element and exports it as a professional, high-quality PDF.
 */
export const exportToPDF = async (elementId, fileName = 'Dashboard_Snapshot') => {
  const element = document.getElementById(elementId);
  if (!element) return false;

  const originalStyle = element.getAttribute('style');

  try {
    // 1. Prepare for capture: Disable animations and force stable layout
    const styleTag = document.createElement('style');
    styleTag.innerHTML = `
      * { 
        animation: none !important; 
        transition: none !important; 
        opacity: 1 !important; 
        transform: none !important; 
        backdrop-filter: none !important;
        -webkit-backdrop-filter: none !important;
      }
      .glass-panel { 
        background: #fff !important; 
        box-shadow: 0 4px 12px rgba(0,0,0,0.05) !important;
        border: 1px solid #eee !important;
      }
      .metric-value {
        background: none !important;
        -webkit-background-clip: border-box !important;
        background-clip: border-box !important;
        -webkit-text-fill-color: #111 !important;
        color: #111 !important;
        font-weight: 800 !important;
      }
      .date-presets-container, .btn-light, .DashboardActions { display: none !important; }
    `;
    document.head.appendChild(styleTag);
    
    // Hide export actions specifically (using both class and direct style)
    const actions = element.querySelector('.d-flex.gap-2.ms-2');
    if (actions) actions.style.setProperty('display', 'none', 'important');

    // Force fixed width for layout stability during capture
    element.style.width = '1200px';
    element.style.minWidth = '1200px';
    
    // Scroll to top to ensure html2canvas starts from the beginning
    window.scrollTo(0, 0);

    // 2. Capture using html2canvas with high scale
    const canvas = await html2canvas(element, {
      scale: 2.5, // slightly lower than 3 for extreme stability but still Sharp
      useCORS: true,
      logging: false,
      backgroundColor: '#f8f9fa',
      width: 1200,
      removeContainer: true
    });
    
    // 3. Cleanup temporary styles
    document.head.removeChild(styleTag);
    if (actions) actions.style.display = 'flex';
    element.setAttribute('style', originalStyle || '');

    // 4. Generate PDF
    const imgData = canvas.toDataURL('image/png', 1.0);
    const pdf = new jsPDF('p', 'mm', 'a4');
    
    const pdfWidth = pdf.internal.pageSize.getWidth();
    const pdfHeight = pdf.internal.pageSize.getHeight();
    
    const imgProps = pdf.getImageProperties(imgData);
    const imgHeight = (imgProps.height * pdfWidth) / imgProps.width;

    // Use 'SLOW' compression for maximum quality in the final PDF
    pdf.addImage(imgData, 'PNG', 0, 0, pdfWidth, imgHeight, undefined, 'SLOW');
    
    pdf.save(`${fileName}_${new Date().toISOString().split('T')[0]}.pdf`);
    return true;
  } catch (error) {
    console.error('PDF Export Error:', error);
    // Cleanup on error
    if (element) element.setAttribute('style', originalStyle || '');
    return false;
  }
};
