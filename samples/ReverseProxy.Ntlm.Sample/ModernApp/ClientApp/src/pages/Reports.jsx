import { useEffect, useState } from 'react';
import { api, statusLabels } from '../api.js';
import { formatCurrency } from '../shared.jsx';

export default function Reports() {
  const [report, setReport] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    // Requires the Admin role; the API answers 403 otherwise.
    api('/reports/permits').then(setReport).catch((e) => setError(e.message));
  }, []);

  return (
    <>
      <h1>Reports</h1>
      {error && <p className="error">{error}</p>}
      {report && (
        <>
          <div className="stats">
            <div className="stat"><div className="stat-value">{report.total}</div><div className="stat-label">Applications</div></div>
            <div className="stat"><div className="stat-value">{Math.round(report.approvalRate * 100)}%</div><div className="stat-label">Approval rate</div></div>
            <div className="stat"><div className="stat-value">{formatCurrency(report.feesInvoiced)}</div><div className="stat-label">Fees invoiced</div></div>
          </div>
          <div className="columns">
            <Breakdown title="By status" data={report.byStatus} labels={statusLabels} />
            <Breakdown title="By type" data={report.byType} />
          </div>
        </>
      )}
    </>
  );
}

function Breakdown({ title, data, labels = {} }) {
  const max = Math.max(...Object.values(data));
  return (
    <section className="card">
      <h2>{title}</h2>
      {Object.entries(data).map(([key, value]) => (
        <div key={key} className="bar-row">
          <span className="bar-label">{labels[key] ?? key}</span>
          <span className="bar"><span style={{ width: `${(value / max) * 100}%` }} /></span>
          <span className="bar-value">{value}</span>
        </div>
      ))}
    </section>
  );
}
