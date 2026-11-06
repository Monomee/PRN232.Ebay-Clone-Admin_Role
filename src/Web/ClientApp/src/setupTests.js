// jsdom (Jest 27) lacks TextEncoder/TextDecoder, which jspdf needs at import time.
import { TextEncoder, TextDecoder } from 'util';

global.TextEncoder ??= TextEncoder;
global.TextDecoder ??= TextDecoder;

// public/index.html has <base href="/">; some modules read it at import time.
if (!document.querySelector('base')) {
  const base = document.createElement('base');
  base.href = '/';
  document.head.appendChild(base);
}
