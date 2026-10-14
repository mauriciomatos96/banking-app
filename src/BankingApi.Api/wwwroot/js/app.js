import { api, events, getSession, clearSession, newIdempotencyKey } from './api.js';
import { h, icon, money, dateTime, toast, openDialog, submitting, initials } from './ui.js';

const root = document.getElementById('app');

const TYPE_LABELS = {
  Deposit: 'Depósito',
  Withdrawal: 'Retiro',
  TransferIn: 'Transferencia recibida',
  TransferOut: 'Transferencia enviada'
};
const isIncoming = type => type === 'Deposit' || type === 'TransferIn';
const CURRENCIES = ['DOP', 'USD', 'EUR'];

// ---------- Router ----------

const routes = [
  { pattern: /^#\/login$/, view: loginView, public: true },
  { pattern: /^#\/accounts$/, view: accountsView },
  { pattern: /^#\/accounts\/([0-9a-f-]{36})$/i, view: accountDetailView },
  { pattern: /^#\/admin$/, view: adminView, admin: true }
];

let renderId = 0;

async function render() {
  const id = ++renderId;
  const session = getSession();
  const hash = location.hash || '#/accounts';
  const route = routes.find(r => r.pattern.test(hash));

  if (!route) return navigate(session ? '#/accounts' : '#/login');
  if (!route.public && !session) return navigate('#/login');
  if (route.public && session) return navigate('#/accounts');
  if (route.admin && session.user.role !== 'Admin') return navigate('#/accounts');

  const params = hash.match(route.pattern).slice(1);
  // Each view receives `isCurrent` so a slow request cannot overwrite a newer page.
  await route.view({ params, session, isCurrent: () => id === renderId });
}

function navigate(hash) {
  if (location.hash === hash) render();
  else location.hash = hash;
}

events.onUnauthorized = () => {
  toast('Tu sesión expiró. Inicia sesión de nuevo.', 'error');
  navigate('#/login');
};

window.addEventListener('hashchange', render);
render();

// ---------- Layout ----------

function layout(session, active, ...content) {
  const link = (hash, label) => h('a', { href: hash, class: active === hash ? 'active' : null }, label);
  const logout = () => { clearSession(); navigate('#/login'); };

  root.replaceChildren(
    h('header', { class: 'topbar' },
      h('div', { class: 'topbar-inner' },
        h('a', { class: 'brand', href: '#/accounts' }, h('span', { class: 'brand-mark' }, icon('bank')), 'Banking Demo'),
        h('nav', { class: 'nav' },
          link('#/accounts', 'Mis cuentas'),
          session.user.role === 'Admin' && link('#/admin', 'Administración')),
        h('div', { class: 'user' },
          h('span', { class: 'name' }, session.user.fullName),
          h('span', { class: 'avatar', title: session.user.email }, initials(session.user.fullName)),
          h('button', { class: 'btn btn-ghost btn-sm', type: 'button', onClick: logout }, 'Salir')))),
    h('main', { class: 'container' }, ...content));
}

function loading(session, active) {
  layout(session, active, h('div', { class: 'loading' }, 'Cargando…'));
}

function failed(session, active, error) {
  layout(session, active, h('div', { class: 'card empty' },
    h('h3', {}, 'No se pudo cargar la información'),
    h('p', { class: 'muted' }, error.message),
    h('button', { class: 'btn', type: 'button', onClick: render }, 'Reintentar')));
}

// ---------- Login / register ----------

function loginView() {
  let mode = 'login';

  const errorBox = h('div', { class: 'form-error hidden', role: 'alert' });
  const nameField = h('div', { class: 'field hidden' },
    h('label', { for: 'fullName' }, 'Nombre completo'),
    h('input', { id: 'fullName', name: 'fullName', autocomplete: 'name', minLength: 2, maxLength: 150 }));
  const passwordHint = h('small', { class: 'hidden' }, 'Mínimo 8 caracteres.');
  const submit = h('button', { class: 'btn btn-gold', type: 'submit', style: 'width:100%' }, 'Iniciar sesión');

  const form = h('form', { novalidate: false },
    errorBox,
    nameField,
    h('div', { class: 'field' },
      h('label', { for: 'email' }, 'Correo electrónico'),
      h('input', { id: 'email', name: 'email', type: 'email', autocomplete: 'email', required: true })),
    h('div', { class: 'field' },
      h('label', { for: 'password' }, 'Contraseña'),
      h('input', { id: 'password', name: 'password', type: 'password', autocomplete: 'current-password', required: true, minLength: 1 }),
      passwordHint),
    submit);

  const tabButton = (value, label) => h('button', {
    type: 'button', role: 'tab', 'aria-selected': String(value === mode),
    onClick: () => setMode(value)
  }, label);
  const tabs = h('div', { class: 'tabs', role: 'tablist' });

  function setMode(value) {
    mode = value;
    const registering = mode === 'register';
    tabs.replaceChildren(tabButton('login', 'Iniciar sesión'), tabButton('register', 'Registrarse'));
    nameField.classList.toggle('hidden', !registering);
    nameField.querySelector('input').required = registering;
    passwordHint.classList.toggle('hidden', !registering);
    form.password.minLength = registering ? 8 : 1;
    form.password.autocomplete = registering ? 'new-password' : 'current-password';
    submit.textContent = registering ? 'Crear usuario' : 'Iniciar sesión';
    errorBox.classList.add('hidden');
  }
  setMode('login');

  form.addEventListener('submit', async event => {
    event.preventDefault();
    const { fullName, email, password } = form;
    const session = await submitting(form, () => mode === 'register'
      ? api.register(fullName.value.trim(), email.value.trim(), password.value)
      : api.login(email.value.trim(), password.value));
    if (!session) return;
    toast(`Bienvenido, ${session.user.fullName.split(' ')[0]}.`);
    navigate('#/accounts');
  });

  root.replaceChildren(
    h('div', { class: 'auth' },
      h('section', { class: 'auth-hero' },
        h('div', {},
          h('a', { class: 'brand', href: '#/login' }, h('span', { class: 'brand-mark' }, icon('bank')), 'Banking Demo'),
          h('h1', {}, 'Tu dinero, ', h('span', {}, 'bajo control.')),
          h('p', {}, 'Abre cuentas, deposita, retira y transfiere al instante.'),
          h('ul', {},
            h('li', {}, 'Transferencias atómicas: se completan enteras o no se hacen.'),
            h('li', {}, 'Protección contra cobros duplicados.'),
            h('li', {}, 'Historial completo de cada movimiento.'))),
        h('small', {}, 'Proyecto de demostración. No uses datos reales.')),
      h('section', { class: 'auth-panel' },
        h('div', { class: 'auth-card' },
          h('h2', {}, 'Bienvenido'),
          h('p', { class: 'muted' }, 'Ingresa a tu banca en línea.'),
          tabs,
          form,
          h('div', { class: 'demo-hint' }, '¿Primera vez? Elige "Registrarse" para crear tu usuario.')))));
  form.email.focus();
}

// ---------- Accounts list ----------

async function accountsView({ session, isCurrent }) {
  loading(session, '#/accounts');
  let accounts;
  try {
    accounts = await api.listAccounts();
  } catch (error) {
    if (isCurrent()) failed(session, '#/accounts', error);
    return;
  }
  if (!isCurrent()) return;

  const totals = CURRENCIES
    .map(currency => ({ currency, total: accounts.filter(a => a.currency === currency).reduce((sum, a) => sum + a.balance, 0) }))
    .filter(t => accounts.some(a => a.currency === t.currency));

  const newAccountButton = h('button', { class: 'btn btn-gold', type: 'button', onClick: openNewAccountDialog },
    icon('plus'), 'Abrir cuenta');

  layout(session, '#/accounts',
    h('div', { class: 'page-head' },
      h('div', {},
        h('h1', {}, `Hola, ${session.user.fullName.split(' ')[0]}`),
        h('p', { class: 'muted' }, accounts.length
          ? `Tienes ${accounts.length} ${accounts.length === 1 ? 'cuenta' : 'cuentas'}.`
          : 'Todavía no tienes cuentas.')),
      newAccountButton),
    totals.length > 0 && h('div', { class: 'summary' },
      totals.map(t => h('div', { class: 'card' },
        h('div', { class: 'label' }, `Saldo total ${t.currency}`),
        h('div', { class: 'value' }, money(t.total, t.currency))))),
    accounts.length
      ? h('div', { class: 'accounts' }, accounts.map(accountCard))
      : h('div', { class: 'card empty' },
          h('h3', {}, 'Abre tu primera cuenta'),
          h('p', { class: 'muted' }, 'Elige una moneda y empieza a operar en segundos.'),
          h('button', { class: 'btn btn-gold', type: 'button', onClick: openNewAccountDialog }, icon('plus'), 'Abrir cuenta')));
}

function accountCard(account) {
  return h('a', { class: 'account-card', href: `#/accounts/${account.id}` },
    h('div', { class: 'top' },
      h('span', { class: 'currency' }, account.currency),
      statusBadge(account.status)),
    h('div', {},
      h('div', { class: 'number mono' }, formatAccountNumber(account.accountNumber)),
      h('div', { class: 'balance' }, money(account.balance, account.currency))));
}

function openNewAccountDialog() {
  openDialog('Abrir cuenta', close => {
    const form = h('form', {},
      h('div', { class: 'form-error hidden', role: 'alert' }),
      h('div', { class: 'field' },
        h('label', { for: 'currency' }, 'Moneda'),
        h('select', { id: 'currency', name: 'currency' },
          h('option', { value: 'DOP' }, 'Pesos dominicanos (DOP)'),
          h('option', { value: 'USD' }, 'Dólares estadounidenses (USD)'),
          h('option', { value: 'EUR' }, 'Euros (EUR)'))),
      h('p', { class: 'muted' }, 'La cuenta se crea con saldo cero y un número de 10 dígitos.'),
      h('div', { class: 'dialog-foot' },
        h('button', { class: 'btn btn-ghost', type: 'button', onClick: close }, 'Cancelar'),
        h('button', { class: 'btn btn-gold', type: 'submit' }, 'Crear cuenta')));

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const account = await submitting(form, () => api.createAccount(form.currency.value));
      if (!account) return;
      close();
      toast(`Cuenta ${formatAccountNumber(account.accountNumber)} creada.`);
      navigate(`#/accounts/${account.id}`);
    });
    return form;
  });
}

// ---------- Account detail ----------

async function accountDetailView({ params: [accountId], session, isCurrent }, state = { page: 1, type: '' }) {
  if (state.page === 1 && !state.type) loading(session, '#/accounts');

  let account, history, myAccounts;
  try {
    [account, history, myAccounts] = await Promise.all([
      api.getAccount(accountId),
      api.history(accountId, { page: state.page, type: state.type }),
      api.listAccounts()
    ]);
  } catch (error) {
    if (isCurrent()) failed(session, '#/accounts', error);
    return;
  }
  if (!isCurrent()) return;

  const reload = (next = state) => accountDetailView({ params: [accountId], session, isCurrent }, next);
  const frozen = account.status === 'Frozen';

  const copyNumber = async () => {
    try {
      await navigator.clipboard.writeText(account.accountNumber);
      toast('Número de cuenta copiado.');
    } catch {
      toast(`Número de cuenta: ${account.accountNumber}`);
    }
  };

  const typeFilter = h('select', { 'aria-label': 'Filtrar por tipo', onChange: e => reload({ page: 1, type: e.target.value }) },
    h('option', { value: '' }, 'Todos los movimientos'),
    Object.entries(TYPE_LABELS).map(([value, label]) => h('option', { value, selected: value === state.type }, label)));

  layout(session, '#/accounts',
    h('a', { class: 'back', href: '#/accounts' }, icon('back', 16), 'Mis cuentas'),
    h('section', { class: 'card detail-head' },
      h('div', {},
        h('div', { class: 'meta' },
          h('span', {}, `Cuenta ${account.currency}`),
          statusBadge(account.status)),
        h('div', { class: 'balance' }, money(account.balance, account.currency)),
        h('div', { class: 'meta' },
          h('span', { class: 'mono' }, formatAccountNumber(account.accountNumber)),
          h('button', { class: 'icon-btn', type: 'button', title: 'Copiar número', 'aria-label': 'Copiar número de cuenta', onClick: copyNumber }, icon('copy', 16)),
          h('span', {}, `Abierta el ${dateTime(account.createdAt)}`))),
      h('div', { class: 'actions' },
        h('button', { class: 'btn btn-gold', type: 'button', disabled: frozen, onClick: () => moneyDialog('deposit', account, reload) }, icon('down'), 'Depositar'),
        h('button', { class: 'btn btn-on-dark', type: 'button', disabled: frozen, onClick: () => moneyDialog('withdraw', account, reload) }, icon('up'), 'Retirar'),
        h('button', { class: 'btn btn-on-dark', type: 'button', disabled: frozen, onClick: () => transferDialog(account, myAccounts, reload) }, icon('swap'), 'Transferir'))),
    frozen && h('div', { class: 'form-error', style: 'margin-top:16px' },
      'Esta cuenta está congelada. No se pueden hacer depósitos, retiros ni transferencias.'),
    h('div', { class: 'section-head' },
      h('h2', {}, 'Movimientos'),
      h('div', { class: 'filters' }, typeFilter)),
    historyTable(account, history, page => reload({ ...state, page })));
}

function historyTable(account, history, goToPage) {
  if (history.totalCount === 0)
    return h('div', { class: 'card empty' },
      h('h3', {}, 'Sin movimientos'),
      h('p', { class: 'muted' }, 'Haz tu primer depósito para empezar.'));

  return h('div', { class: 'card', style: 'padding:8px 6px 14px' },
    h('div', { class: 'table-wrap' },
      h('table', {},
        h('thead', {}, h('tr', {},
          h('th', {}, 'Fecha'), h('th', {}, 'Tipo'), h('th', { class: 'hide-sm' }, 'Descripción'), h('th', { class: 'hide-sm' }, 'Cuenta relacionada'),
          h('th', { class: 'num' }, 'Monto'), h('th', { class: 'num hide-sm' }, 'Saldo'))),
        h('tbody', {}, history.items.map(t => {
          const incoming = isIncoming(t.type);
          return h('tr', {},
            h('td', { class: 'date' }, dateTime(t.createdAt)),
            h('td', {}, h('span', { class: `badge ${incoming ? 'badge-in' : 'badge-out'}` }, TYPE_LABELS[t.type] ?? t.type)),
            h('td', { class: 'hide-sm' }, t.description || h('span', { class: 'muted' }, '—')),
            h('td', { class: 'mono hide-sm' }, t.counterpartyAccountNumber ? formatAccountNumber(t.counterpartyAccountNumber) : h('span', { class: 'muted' }, '—')),
            h('td', { class: `num ${incoming ? 'amount-in' : 'amount-out'}` }, `${incoming ? '+' : '−'}${money(t.amount, account.currency)}`),
            h('td', { class: 'num hide-sm' }, money(t.balanceAfter, account.currency)));
        })))),
    pager(history, goToPage));
}

function pager({ page, totalPages, totalCount }, goToPage) {
  if (totalPages <= 1) return null;
  return h('div', { class: 'pager' },
    h('span', { class: 'muted' }, `Página ${page} de ${totalPages} · ${totalCount} registros`),
    h('div', { class: 'btns' },
      h('button', { class: 'btn btn-ghost btn-sm', type: 'button', disabled: page <= 1, onClick: () => goToPage(page - 1) }, 'Anterior'),
      h('button', { class: 'btn btn-ghost btn-sm', type: 'button', disabled: page >= totalPages, onClick: () => goToPage(page + 1) }, 'Siguiente')));
}

// ---------- Money dialogs ----------

function amountField() {
  return h('div', { class: 'field' },
    h('label', { for: 'amount' }, 'Monto'),
    h('input', { id: 'amount', name: 'amount', type: 'number', inputMode: 'decimal', min: '0.01', max: '1000000', step: '0.01', required: true, placeholder: '0.00' }));
}

function descriptionField() {
  return h('div', { class: 'field' },
    h('label', { for: 'description' }, 'Descripción (opcional)'),
    h('input', { id: 'description', name: 'description', maxLength: 200, placeholder: 'Ej.: Pago de alquiler' }));
}

function moneyDialog(kind, account, onDone) {
  const deposit = kind === 'deposit';
  // One key per dialog: if the user presses "Confirmar" again after a network error,
  // the API recognises the retry and does not apply the operation twice.
  const idempotencyKey = newIdempotencyKey();

  openDialog(deposit ? 'Depositar' : 'Retirar', close => {
    const form = h('form', {},
      h('div', { class: 'form-error hidden', role: 'alert' }),
      h('p', { class: 'muted', style: 'margin-top:0' },
        `Cuenta ${formatAccountNumber(account.accountNumber)} · Saldo ${money(account.balance, account.currency)}`),
      amountField(),
      descriptionField(),
      h('div', { class: 'dialog-foot' },
        h('button', { class: 'btn btn-ghost', type: 'button', onClick: close }, 'Cancelar'),
        h('button', { class: 'btn btn-gold', type: 'submit' }, deposit ? 'Depositar' : 'Retirar')));

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const amount = Number(form.amount.value);
      const description = form.description.value.trim() || null;
      const call = deposit ? api.deposit : api.withdraw;
      const tx = await submitting(form, () => call(account.id, amount, description, idempotencyKey));
      if (!tx) return;
      close();
      toast(`${deposit ? 'Depósito' : 'Retiro'} de ${money(tx.amount, account.currency)} realizado.`);
      onDone({ page: 1, type: '' });
    });
    return form;
  });
}

function transferDialog(account, myAccounts, onDone) {
  const idempotencyKey = newIdempotencyKey();
  const ownOthers = myAccounts.filter(a => a.id !== account.id && a.currency === account.currency && a.status === 'Active');

  openDialog('Transferir', close => {
    const destination = h('input', {
      id: 'toAccountNumber', name: 'toAccountNumber', inputMode: 'numeric', pattern: '\\d{10}', maxLength: 10,
      required: true, placeholder: '10 dígitos', class: 'mono', title: 'El número de cuenta tiene 10 dígitos.'
    });

    const form = h('form', {},
      h('div', { class: 'form-error hidden', role: 'alert' }),
      h('p', { class: 'muted', style: 'margin-top:0' },
        `Desde ${formatAccountNumber(account.accountNumber)} · Disponible ${money(account.balance, account.currency)}`),
      h('div', { class: 'field' },
        h('label', { for: 'toAccountNumber' }, 'Cuenta de destino'),
        destination,
        ownOthers.length > 0 && h('small', {}, 'Tus otras cuentas en ', account.currency, ': ',
          ownOthers.map((a, i) => [i > 0 && ', ',
            h('a', { href: '#', onClick: e => { e.preventDefault(); destination.value = a.accountNumber; } }, a.accountNumber)]))),
      amountField(),
      descriptionField(),
      h('div', { class: 'dialog-foot' },
        h('button', { class: 'btn btn-ghost', type: 'button', onClick: close }, 'Cancelar'),
        h('button', { class: 'btn btn-gold', type: 'submit' }, 'Transferir')));

    destination.addEventListener('input', () => { destination.value = destination.value.replace(/\D/g, ''); });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const amount = Number(form.amount.value);
      const description = form.description.value.trim() || null;
      const tx = await submitting(form, () =>
        api.transfer(account.id, destination.value, amount, description, idempotencyKey));
      if (!tx) return;
      close();
      toast(`Transferiste ${money(tx.amount, account.currency)} a la cuenta ${formatAccountNumber(tx.counterpartyAccountNumber)}.`);
      onDone({ page: 1, type: '' });
    });
    return form;
  });
}

// ---------- Admin ----------

async function adminView({ session, isCurrent }, page = 1) {
  if (page === 1) loading(session, '#/admin');

  let result;
  try {
    result = await api.adminAccounts(page);
  } catch (error) {
    if (isCurrent()) failed(session, '#/admin', error);
    return;
  }
  if (!isCurrent()) return;

  const reload = (p = page) => adminView({ session, isCurrent }, p);

  const toggle = async (account, button) => {
    button.disabled = true;
    try {
      const updated = account.status === 'Frozen' ? await api.unfreeze(account.id) : await api.freeze(account.id);
      toast(`Cuenta ${formatAccountNumber(updated.accountNumber)} ${updated.status === 'Frozen' ? 'congelada' : 'activada'}.`);
      reload();
    } catch (error) {
      toast(error.message, 'error');
      button.disabled = false;
    }
  };

  layout(session, '#/admin',
    h('div', { class: 'page-head' },
      h('div', {},
        h('h1', {}, 'Administración'),
        h('p', { class: 'muted' }, `${result.totalCount} cuentas en el banco.`))),
    h('div', { class: 'card', style: 'padding:8px 6px 14px' },
      h('div', { class: 'table-wrap' },
        h('table', {},
          h('thead', {}, h('tr', {},
            h('th', {}, 'Número'), h('th', { class: 'hide-sm' }, 'Moneda'), h('th', {}, 'Estado'), h('th', { class: 'hide-sm' }, 'Creada'),
            h('th', { class: 'num' }, 'Saldo'), h('th', { class: 'num' }, 'Acción'))),
          h('tbody', {}, result.items.map(a => {
            const button = h('button', {
              class: `btn btn-sm ${a.status === 'Frozen' ? '' : 'btn-danger'}`, type: 'button',
              onClick: () => toggle(a, button)
            }, a.status === 'Frozen' ? 'Activar' : 'Congelar');
            return h('tr', {},
              h('td', { class: 'mono' }, formatAccountNumber(a.accountNumber)),
              h('td', { class: 'hide-sm' }, a.currency),
              h('td', {}, statusBadge(a.status)),
              h('td', { class: 'hide-sm' }, dateTime(a.createdAt)),
              h('td', { class: 'num' }, money(a.balance, a.currency)),
              h('td', { class: 'num' }, button));
          })))),
      pager(result, reload)));
}

// ---------- Helpers ----------

function statusBadge(status) {
  return status === 'Frozen'
    ? h('span', { class: 'badge badge-frozen' }, 'Congelada')
    : h('span', { class: 'badge badge-active' }, 'Activa');
}

const formatAccountNumber = number => number.replace(/(\d{3})(\d{3})(\d{4})/, '$1-$2-$3');
