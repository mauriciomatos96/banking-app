// Thin wrapper around the Banking API. Keeps the session in sessionStorage
// (cleared when the tab closes) and turns problem-details responses into ApiError.

const SESSION_KEY = 'banking.session';

export class ApiError extends Error {
  constructor(status, code, message, fieldErrors = {}) {
    super(message);
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

const MESSAGES = {
  insufficient_funds: 'Fondos insuficientes para esta operación.',
  account_frozen: 'La cuenta está congelada. Contacta al administrador.',
  currency_mismatch: 'Las dos cuentas deben tener la misma moneda.',
  same_account: 'La cuenta de origen y destino no pueden ser la misma.',
  invalid_amount: 'El monto debe ser mayor que cero y tener como máximo dos decimales.',
  idempotency_key_reused: 'Esta operación ya se envió con otros datos. Vuelve a intentarlo.',
  concurrent_update: 'La cuenta cambió mientras se procesaba la operación. Inténtalo de nuevo.',
  email_taken: 'Ya existe un usuario con ese correo.',
  unauthorized: 'Correo o contraseña incorrectos.',
  not_found: 'No se encontró la cuenta.'
};

// ---------- Session ----------

export function getSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(SESSION_KEY));
    if (session && new Date(session.expiresAt) > new Date()) return session;
  } catch { /* storage unavailable or corrupted */ }
  clearSession();
  return null;
}

function saveSession(auth) {
  const session = { token: auth.accessToken, expiresAt: auth.expiresAt, user: auth.user };
  try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(session)); } catch { /* ignore */ }
  return session;
}

export function clearSession() {
  try { sessionStorage.removeItem(SESSION_KEY); } catch { /* ignore */ }
}

/** Called when the API says the token is no longer valid. app.js sets it. */
export const events = { onUnauthorized: () => {} };

// ---------- HTTP ----------

async function request(method, path, { body, idempotencyKey, auth = true } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;

  const session = getSession();
  if (auth && session) headers.Authorization = `Bearer ${session.token}`;

  let response;
  try {
    response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, 'network', 'No se pudo conectar con el servidor. Revisa tu conexión.');
  }

  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (response.ok) return data;

  if (response.status === 401 && auth) {
    clearSession();
    events.onUnauthorized();
  }
  throw toApiError(response.status, data);
}

function toApiError(status, problem) {
  if (problem?.errors) {
    // ASP.NET validation errors: { errors: { Field: ["message"] } }
    const fieldErrors = Object.fromEntries(
      Object.entries(problem.errors).map(([field, messages]) => [field.toLowerCase(), messages.join(' ')]));
    return new ApiError(status, 'validation', Object.values(fieldErrors).join(' '), fieldErrors);
  }
  const code = problem?.code ?? 'unknown';
  const message = MESSAGES[code] ?? problem?.title ?? `Error inesperado (${status}).`;
  return new ApiError(status, code, message);
}

// ---------- Endpoints ----------

export const api = {
  async login(email, password) {
    return saveSession(await request('POST', '/api/auth/login', { body: { email, password }, auth: false }));
  },
  async register(fullName, email, password) {
    return saveSession(await request('POST', '/api/auth/register', { body: { fullName, email, password }, auth: false }));
  },

  listAccounts: () => request('GET', '/api/accounts'),
  getAccount: id => request('GET', `/api/accounts/${id}`),
  createAccount: currency => request('POST', '/api/accounts', { body: { currency } }),

  history(id, { page = 1, pageSize = 10, type = '' } = {}) {
    const query = new URLSearchParams({ page, pageSize });
    if (type) query.set('type', type);
    return request('GET', `/api/accounts/${id}/transactions?${query}`);
  },

  deposit: (id, amount, description, key) =>
    request('POST', `/api/accounts/${id}/deposits`, { body: { amount, description }, idempotencyKey: key }),
  withdraw: (id, amount, description, key) =>
    request('POST', `/api/accounts/${id}/withdrawals`, { body: { amount, description }, idempotencyKey: key }),
  transfer: (fromAccountId, toAccountNumber, amount, description, key) =>
    request('POST', '/api/transfers', { body: { fromAccountId, toAccountNumber, amount, description }, idempotencyKey: key }),

  adminAccounts: (page = 1, pageSize = 10) => request('GET', `/api/admin/accounts?page=${page}&pageSize=${pageSize}`),
  freeze: id => request('POST', `/api/admin/accounts/${id}/freeze`),
  unfreeze: id => request('POST', `/api/admin/accounts/${id}/unfreeze`)
};

/** A fresh key per operation; the same key is reused if the user retries that operation. */
export const newIdempotencyKey = () =>
  crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
