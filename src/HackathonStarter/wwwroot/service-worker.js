// Minimal service worker: makes the app installable and shows /offline.html when there is no connection.
// It deliberately caches nothing else: Blazor Server needs the live connection anyway, and stale
// cached pages are the classic PWA bug you don't want on demo day.
const CACHE = 'trace-shell-v2';
const OFFLINE = '/offline.html';

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll([OFFLINE, '/icons/icon-192.png'])));
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))));
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  if (event.request.mode !== 'navigate') return;
  event.respondWith(fetch(event.request).catch(() => caches.match(OFFLINE)));
});
