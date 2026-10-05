import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, statusLabels } from '../api.js';
import { StatusBadge, formatDate } from '../shared.jsx';

export default function Permits() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = searchParams.get('status') ?? '';
  const [permits, setPermits] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    setPermits(null);
    api(status ? `/permits?status=${encodeURIComponent(status)}` : '/permits')
      .then(setPermits)
      .catch((e) => setError(e.message));
  }, [status]);

  const filters = [['', 'All'], ...Object.entries(statusLabels)];

  return (
    <>
      <h1>Building permit applications</h1>

      <div className="tabs" role="tablist">
        {filters.map(([value, label]) => (
          <button
            key={value}
            role="tab"
            aria-selected={status === value}
            className={status === value ? 'active' : ''}
            onClick={() => setSearchParams(value ? { status: value } : {})}
          >
            {label}
          </button>
        ))}
      </div>

      {error && <p className="error">{error}</p>}

      <section className="card flush">
        <table className="table">
          <thead>
            <tr>
              <th>Reference</th>
              <th>Type</th>
              <th>Address</th>
              <th>Applicant</th>
              <th>Submitted</th>
              <th>Officer</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {permits?.map((permit) => (
              <tr key={permit.id}>
                <td><Link to={`/permits/${permit.id}`}>{permit.id}</Link></td>
                <td>{permit.type}</td>
                <td>{permit.address}</td>
                <td>{permit.applicant}</td>
                <td>{formatDate(permit.submitted)}</td>
                <td>{permit.officer ?? <span className="muted">Unassigned</span>}</td>
                <td><StatusBadge status={permit.status} /></td>
              </tr>
            ))}
            {permits?.length === 0 && (
              <tr><td colSpan="7" className="muted">No applications.</td></tr>
            )}
            {!permits && !error && (
              <tr><td colSpan="7" className="muted">Loading…</td></tr>
            )}
          </tbody>
        </table>
      </section>
    </>
  );
}
