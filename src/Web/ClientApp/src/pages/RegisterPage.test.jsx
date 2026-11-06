import React from 'react';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';
import RegisterPage from './RegisterPage';

// Sign-up without a captcha key (dev): a validation error shows the server's own message,
// then a successful sign-up moves on to the email code.
const json = (status, body) => Promise.resolve({
  ok: status < 400, status,
  headers: { get: () => 'application/json' },
  json: () => Promise.resolve(body),
});

let container;
let root;

beforeEach(() => {
  global.IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement('div');
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(() => {
  act(() => root.unmount());
  container.remove();
  delete global.fetch;
});

const flush = () => act(() => new Promise((resolve) => setTimeout(resolve, 0)));
const submit = () => act(async () => { container.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); });

it('shows validation messages, then asks for the email code', async () => {
  const calls = [];
  global.fetch = jest.fn((url, init) => {
    calls.push([url, init?.body && JSON.parse(init.body)]);
    if (url.endsWith('/captcha-config')) return json(200, { turnstileSiteKey: null });
    if (calls.filter(([u]) => u.endsWith('/register')).length === 1)
      return json(400, { title: 'One or more validation errors occurred.', errors: { Email: ['Email đã được sử dụng.'] } });
    return json(200, { success: true, message: 'Đã gửi mã xác minh tới email của bạn.' });
  });

  await act(async () => root.render(<RegisterPage />));
  await flush();
  expect(container.querySelector('iframe')).toBeNull(); // no site key: no captcha widget

  await submit();
  await flush();
  expect(container.textContent).toContain('Email đã được sử dụng.');

  await submit();
  await flush();
  expect(container.querySelector('#reg-code')).not.toBeNull();
  const [, body] = calls.filter(([u]) => u.endsWith('/register'))[1];
  expect(body.role).toBe('Buyer');
  expect(body.cccd).toBeNull(); // empty optional fields are sent as null, not ""
});
