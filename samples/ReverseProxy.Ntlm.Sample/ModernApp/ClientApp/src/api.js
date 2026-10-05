// Served by the LEGACY app (Windows authentication). The browser does the NTLM/Kerberos handshake
// through the gateway, and the response sets the HttpOnly JWT cookie.
export async function signIn() {
  const response = await fetch('/api/users/me', { credentials: 'same-origin' });
  if (!response.ok) {
    throw new Error(`Windows sign-in failed (HTTP ${response.status}).`);
  }
  return response.json();
}

export class ApiError extends Error {
  constructor(status, body) {
    super(status === 403 ? 'You do not have permission to do this.' : `Request failed (HTTP ${status}).`);
    this.status = status;
    this.body = body;
  }
}

// Calls /api{path} through the gateway, which sends it to whichever app currently implements the endpoint.
// For the modern app, the gateway moves the JWT cookie into the Authorization header.
// When the token has expired, sign in again through the legacy app and retry once.
export async function api(path, { method = 'GET', body } = {}) {
  const send = () => fetch(`/api${path}`, {
    method,
    credentials: 'same-origin',
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });

  let response = await send();
  if (response.status === 401) {
    await signIn();
    response = await send();
  }

  const json = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  if (!response.ok) {
    throw new ApiError(response.status, json);
  }
  return json;
}

export const statusLabels = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  NeedsCompletion: 'Needs completion',
  Approved: 'Approved',
  Rejected: 'Rejected',
};
