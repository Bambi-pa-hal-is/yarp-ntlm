import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api.js';
import { StatusBadge, formatCurrency, formatDate, formatDateTime, useUser } from '../shared.jsx';

const openStatuses = ['Submitted', 'UnderReview', 'NeedsCompletion'];

export default function PermitDetails() {
  const { id } = useParams();
  const user = useUser();
  const [permit, setPermit] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    api(`/permits/${encodeURIComponent(id)}`)
      .then(setPermit)
      .catch((e) => setError(e.status === 404 ? `Application ${id} was not found.` : e.message));
  }, [id]);

  if (error) {
    return <><BackLink /><p className="error">{error}</p></>;
  }
  if (!permit) {
    return <><BackLink /><p className="muted">Loading…</p></>;
  }

  return (
    <>
      <BackLink />
      <div className="title-row">
        <h1>{permit.type} · {permit.address}</h1>
        <StatusBadge status={permit.status} />
      </div>
      <p className="muted">{permit.id} · submitted {formatDate(permit.submitted)}</p>

      <div className="columns">
        <section className="card">
          <h2>Application</h2>
          <dl className="details">
            <dt>Applicant</dt><dd>{permit.applicant}</dd>
            <dt>Property</dt><dd>{permit.property}</dd>
            <dt>Address</dt><dd>{permit.address}</dd>
            <dt>Officer</dt><dd>{permit.officer ?? 'Unassigned'}</dd>
            <dt>Fee</dt><dd>{formatCurrency(permit.fee)}</dd>
            {permit.legacyCaseNumber && (
              <>
                <dt>Legacy case</dt>
                {/* Full page load to the legacy app. */}
                <dd><a href={`/cases/${permit.legacyCaseNumber}`}>{permit.legacyCaseNumber} ↗</a></dd>
              </>
            )}
          </dl>
          <p>{permit.description}</p>
        </section>

        <section className="card">
          <h2>Timeline</h2>
          <ol className="timeline">
            {[...permit.timeline].reverse().map((event, index) => (
              <li key={index}>
                <div className="small muted">{formatDateTime(event.at)} · {event.by}</div>
                <div>{event.text}</div>
              </li>
            ))}
          </ol>
        </section>
      </div>

      {openStatuses.includes(permit.status) && (
        user.hasRole('PermitOfficer')
          ? <DecisionForm permit={permit} onDecided={setPermit} />
          : <p className="muted card">Only building permit officers can make decisions.</p>
      )}
    </>
  );
}

function BackLink() {
  return <p><Link to="/permits">← All applications</Link></p>;
}

function DecisionForm({ permit, onDecided }) {
  const [decision, setDecision] = useState('Approved');
  const [comment, setComment] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  async function submit(event) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      onDecided(await api(`/permits/${permit.id}/decision`, { method: 'POST', body: { decision, comment } }));
      setComment('');
    } catch (e) {
      setError(e.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="card">
      <h2>Decision</h2>
      <form onSubmit={submit} className="decision">
        <fieldset>
          {[['Approved', 'Approve'], ['NeedsCompletion', 'Request completion'], ['Rejected', 'Reject']].map(([value, label]) => (
            <label key={value}>
              <input type="radio" name="decision" value={value} checked={decision === value} onChange={() => setDecision(value)} />
              {label}
            </label>
          ))}
        </fieldset>
        <label className="block">
          Comment to applicant
          <textarea value={comment} onChange={(e) => setComment(e.target.value)} rows={3} />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" className="primary" disabled={saving}>{saving ? 'Saving…' : 'Register decision'}</button>
      </form>
    </section>
  );
}
