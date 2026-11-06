import React from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import App from './App';

// No server in Jest: the shell's live-notification connection is not under test here.
jest.mock('./hooks/useNotificationHub', () => ({ useNotificationHub: () => {} }));

it('renders without crashing', async () => {
  const div = document.createElement('div');
  const root = createRoot(div);
  root.render(
    <MemoryRouter>
      <App />
    </MemoryRouter>);
  await new Promise(resolve => setTimeout(resolve, 1000));
});
