import { notify } from './toast';

test('notify shows a typed toast as plain text and removes it on close and on timeout', () => {
  jest.useFakeTimers();
  notify('<b>Saved</b>', 'success');
  notify('Failed', 'error');

  const toasts = document.querySelectorAll('.app-toast');
  expect(toasts).toHaveLength(2);
  expect(toasts[0].classList.contains('success')).toBe(true);
  expect(toasts[0].querySelector('span').textContent).toBe('<b>Saved</b>'); // never rendered as HTML
  expect(toasts[1].getAttribute('role')).toBe('alert');

  toasts[1].querySelector('.btn-close').click();
  expect(document.querySelectorAll('.app-toast')).toHaveLength(1);

  jest.runAllTimers();
  expect(document.querySelectorAll('.app-toast')).toHaveLength(0);
  jest.useRealTimers();
});
