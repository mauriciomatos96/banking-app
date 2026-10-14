// Small DOM helpers. Everything is built with createElement/textContent,
// never innerHTML, so data from the API (names, descriptions) cannot inject HTML.

export function h(tag, props = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props ?? {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key.startsWith('on')) el.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key in el && typeof value !== 'string') el[key] = value;
    else el.setAttribute(key, value === true ? '' : value);
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

/** Inline SVG icon from a path list (24x24 viewBox). */
export function icon(name, size = 18) {
  const paths = {
    bank: 'M12 2 2 7v2h20V7zM4 11v8h3v-8zm6.5 0v8h3v-8zM17 11v8h3v-8zM2 21v2h20v-2z',
    copy: 'M16 1H4a2 2 0 0 0-2 2v14h2V3h12zm3 4H8a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2zm0 16H8V7h11z',
    close: 'M19 6.4 17.6 5 12 10.6 6.4 5 5 6.4 10.6 12 5 17.6 6.4 19 12 13.4 17.6 19 19 17.6 13.4 12z',
    back: 'M20 11H7.8l5.6-5.6L12 4l-8 8 8 8 1.4-1.4L7.8 13H20z',
    plus: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6z',
    down: 'M11 4h2v12l5.5-5.5 1.4 1.4L12 19.8l-7.9-7.9 1.4-1.4L11 16z',
    up: 'M13 20h-2V8l-5.5 5.5-1.4-1.4L12 4.2l7.9 7.9-1.4 1.4L13 8z',
    swap: 'M7 11 3 7l4-4v3h11v2H7zm10 2 4 4-4 4v-3H6v-2h11z'
  };
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('width', size);
  svg.setAttribute('height', size);
  svg.setAttribute('fill', 'currentColor');
  svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
  path.setAttribute('d', paths[name]);
  svg.append(path);
  return svg;
}

export function money(amount, currency) {
  return new Intl.NumberFormat('es-DO', { style: 'currency', currency }).format(amount);
}

export function dateTime(iso) {
  return new Intl.DateTimeFormat('es-DO', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
}

export function toast(message, type = 'info') {
  const el = h('div', { class: `toast ${type}` }, message);
  document.getElementById('toasts').append(el);
  setTimeout(() => el.remove(), 4500);
}

/**
 * Opens a modal <dialog>. `build(close)` returns the body content.
 * The dialog removes itself from the DOM when closed.
 */
export function openDialog(title, build) {
  const dialog = h('dialog', { 'aria-label': title });
  // Remove right away instead of waiting for the async "close" event, so a closed
  // dialog never lingers in the DOM. The listener below still handles the Esc key.
  const close = () => { dialog.close(); dialog.remove(); };
  dialog.append(
    h('div', { class: 'dialog-head' },
      h('h3', {}, title),
      h('button', { class: 'icon-btn', type: 'button', 'aria-label': 'Cerrar', onClick: close }, icon('close'))),
    h('div', { class: 'dialog-body' }, build(close)));
  dialog.addEventListener('close', () => dialog.remove());
  document.body.append(dialog);
  dialog.showModal();
  dialog.querySelector('input, select')?.focus();
  return dialog;
}

/**
 * Disables a form's submit button while `task` runs and shows any error inside the form.
 * Resolves to the task's result, or to undefined if it failed (the error is already shown).
 */
export async function submitting(form, task) {
  const button = form.querySelector('[type=submit]');
  const errorBox = form.querySelector('.form-error');
  button.disabled = true;
  errorBox?.classList.add('hidden');
  try {
    return await task();
  } catch (error) {
    if (errorBox) {
      errorBox.textContent = error.message;
      errorBox.classList.remove('hidden');
    } else {
      toast(error.message, 'error');
    }
    return undefined;
  } finally {
    button.disabled = false;
  }
}

export const initials = name =>
  name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0].toUpperCase()).join('');
