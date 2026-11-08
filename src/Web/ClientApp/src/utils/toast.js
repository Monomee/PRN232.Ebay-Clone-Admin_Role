// Drop-in replacement for alert(): a non-blocking toast in the top-right corner (styles in custom.css).
const ICONS = { success: 'bi-check-circle-fill', error: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };

export function notify(message, type = 'info', timeout = 4500) {
  let stack = document.querySelector('.app-toast-stack');
  if (!stack) {
    stack = document.createElement('div');
    stack.className = 'app-toast-stack';
    stack.setAttribute('aria-live', 'polite');
    document.body.appendChild(stack);
  }
  const toast = document.createElement('div');
  toast.className = `app-toast ${ICONS[type] ? type : 'info'}`;
  toast.setAttribute('role', type === 'error' ? 'alert' : 'status');
  const icon = document.createElement('i');
  icon.className = `bi ${ICONS[type] || ICONS.info}`;
  icon.setAttribute('aria-hidden', 'true');
  const text = document.createElement('span');
  text.textContent = typeof message === 'string' ? message : String(message ?? ''); // textContent: never HTML
  const close = document.createElement('button');
  close.type = 'button';
  close.className = 'btn-close';
  close.setAttribute('aria-label', 'Dismiss');
  const remove = () => toast.remove();
  close.onclick = remove;
  toast.append(icon, text, close);
  stack.appendChild(toast);
  setTimeout(remove, timeout);
}
