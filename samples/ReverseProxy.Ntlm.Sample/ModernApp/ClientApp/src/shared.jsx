import { createContext, useContext } from 'react';
import { statusLabels } from './api.js';

export const UserContext = createContext(null);

// The signed-in user from /api/users/me, plus hasRole(role).
export function useUser() {
  return useContext(UserContext);
}

export function StatusBadge({ status }) {
  return <span className={`badge badge-${status}`}>{statusLabels[status] ?? status}</span>;
}

export function formatDate(value) {
  return new Date(value).toLocaleDateString('sv-SE');
}

export function formatDateTime(value) {
  return new Date(value).toLocaleString('sv-SE', { dateStyle: 'short', timeStyle: 'short' });
}

export function formatCurrency(value) {
  return new Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK', maximumFractionDigits: 0 }).format(value);
}
