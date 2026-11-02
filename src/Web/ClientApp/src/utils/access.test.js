import { canAccess } from './access';

it('opens a page by system role or by a granted permission only', () => {
  const qaLead = { role: 'QA Lead', permissions: ['ManageOrders'] };
  expect(canAccess(qaLead, ['SuperAdmin', 'Support'], 'ManageOrders')).toBe(true);
  expect(canAccess(qaLead, ['SuperAdmin', 'Support'], 'ManageDisputes')).toBe(false);
  expect(canAccess(qaLead, ['SuperAdmin'])).toBe(false);
  expect(canAccess({ role: 'Support' }, ['SuperAdmin', 'Support'], 'ManageOrders')).toBe(true);
  expect(canAccess({ role: 'Seller' }, ['SuperAdmin', 'Support'], 'ViewDashboard')).toBe(false);
});
