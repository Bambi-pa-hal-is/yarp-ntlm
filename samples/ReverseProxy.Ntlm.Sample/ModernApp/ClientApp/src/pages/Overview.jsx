import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { StatusBadge, formatDate, useUser } from '../shared.jsx';

export default function Overview() {
  const user = useUser();
  const [permits, setPermits] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    api('/permits').then(setPermits).catch((e) => setError(e.message));
  }, []);

  const count = (status) => permits?.filter((permit) => permit.status === status).length ?? '–';
  const firstName = user.name.split(' ')[0];
  const mine = permits?.filter((permit) => permit.officer === user.name && ['Submitted', 'UnderReview', 'NeedsCompletion'].includes(permit.status));
  const unassigned = permits?.filter((permit) => !permit.officer);

  return (
    <>
      <h1>Good day, {firstName}</h1>
      <p className="muted">Building permit applications for Ekdala Municipality.</p>

      {error && <p className="error">{error}</p>}

      <div className="stats">
        <Stat label="New applications" value={count('Submitted')} to="/permits?status=Submitted" />
        <Stat label="Under review" value={count('UnderReview')} to="/permits?status=UnderReview" />
        <Stat label="Waiting for applicant" value={count('NeedsCompletion')} to="/permits?status=NeedsCompletion" />
        <Stat label="Decided" value={permits ? count('Approved') + count('Rejected') : '–'} to="/permits?status=Approved" />
      </div>

      <div className="columns">
        <section className="card">
          <h2>Unassigned applications</h2>
          <PermitList permits={unassigned} empty="All applications have an officer." />
        </section>
        <section className="card">
          <h2>Your open cases</h2>
          <PermitList permits={mine} empty="You have no open building permit cases." />
        </section>
      </div>

      <section className="card info">
        <h2>Where is everything else?</h2>
        <p>
          Building permits were moved to this system on 1 September 2026. All other cases (social services, education,
          environment & health, technical services) and the resident register are still in the{' '}
          <a href="/">case management system</a>. You are signed in to both with your Windows account.
        </p>
      </section>
    </>
  );
}

function Stat({ label, value, to }) {
  return (
    <Link to={to} className="stat">
      <div className="stat-value">{value}</div>
      <div className="stat-label">{label}</div>
    </Link>
  );
}

function PermitList({ permits, empty }) {
  if (!permits) {
    return <p className="muted">Loading…</p>;
  }
  if (permits.length === 0) {
    return <p className="muted">{empty}</p>;
  }
  return (
    <ul className="permit-list">
      {permits.map((permit) => (
        <li key={permit.id}>
          <Link to={`/permits/${permit.id}`}>
            <span>
              <strong>{permit.type}</strong> · {permit.address}
              <span className="muted small"> · {formatDate(permit.submitted)}</span>
            </span>
            <StatusBadge status={permit.status} />
          </Link>
        </li>
      ))}
    </ul>
  );
}
