import React from 'react';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';
import SensitiveLogsPage from './SensitiveLogsPage';
import { sensitiveLogService } from '../services/sensitiveLogService';

jest.mock('../services/sensitiveLogService', () => ({
    sensitiveLogService: { getSummary: jest.fn(), getSensitiveLogs: jest.fn(), decryptLog: jest.fn() }
}));

let container;
let root;
let errorSpy;
const result = {
    id: 1, decryptedData: '{"accountNumber":"private-account"}', encryptedData: 'gcm:v1:fixture',
    integrityVerified: true, encryptionAlgorithm: 'AES-256-GCM', decryptedBy: '42',
    decryptedAt: '2026-10-28T00:00:00Z', dataHash: 'test-hash'
};

beforeEach(async () => {
    global.IS_REACT_ACT_ENVIRONMENT = true;
    jest.useFakeTimers();
    errorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    sensitiveLogService.getSummary.mockResolvedValue({ totalLogs: 1, piiCount: 0, financialCount: 1 });
    sensitiveLogService.getSensitiveLogs.mockResolvedValue({
        items: [{ id: 1, actorId: 42, action: 'RequestWithdrawal', targetEntity: 'Seller',
            dataClassification: 'Financial', encryptedData: 'gcm:v1:fixture', encryptionAlgorithm: 'AES-256-GCM', createdAt: '2026-10-28T00:00:00Z' }],
        totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false
    });
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    await act(async () => root.render(<SensitiveLogsPage />));
});

afterEach(() => {
    act(() => root.unmount());
    container.remove();
    jest.clearAllTimers();
    jest.useRealTimers();
    errorSpy.mockRestore();
    jest.clearAllMocks();
    delete global.IS_REACT_ACT_ENVIRONMENT;
});

async function decrypt() {
    await act(async () => container.querySelector('button[title="SuperAdmin only permission to decrypt this data"]').click());
}

it('keeps plaintext hidden until an authenticated, verified result is explicitly requested', async () => {
    sensitiveLogService.decryptLog.mockResolvedValue(result);
    expect(container.textContent).not.toContain('private-account');
    await decrypt();
    expect(container.querySelector('.modal').textContent).toContain('private-account');
    expect(container.querySelector('.toast-body').textContent).toContain('decryption successful');
});

it('rejects an unverified result even if the response contains plaintext', async () => {
    sensitiveLogService.decryptLog.mockResolvedValue({ ...result, integrityVerified: false });
    await decrypt();
    expect(container.querySelector('.modal')).toBeNull();
    expect(container.textContent).not.toContain('private-account');
    expect(container.querySelector('.toast-body').textContent).toContain('Unable to decrypt');
});

it('clears previously decrypted data when a later request fails', async () => {
    sensitiveLogService.decryptLog.mockResolvedValueOnce(result).mockRejectedValueOnce(new Error('HTTP 403'));
    await decrypt();
    expect(container.textContent).toContain('private-account');
    await decrypt();
    expect(container.querySelector('.modal')).toBeNull();
    expect(container.textContent).not.toContain('private-account');
    expect(container.querySelector('.toast-body').textContent).toContain('Unable to decrypt');
});
