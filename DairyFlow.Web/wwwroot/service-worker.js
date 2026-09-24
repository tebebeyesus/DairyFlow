// DairyFlow Service Worker - Offline-first strategy
const CACHE_VERSION = 'dairyflow-v5';
const API_BASE = '/api';

// Assets to pre-cache on install (app shell). Only list files that actually exist —
// but see install() below: each is cached independently so a stray 404 can't wipe the shell.
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/css/dairyflow.css',
  '/css/dashboard.css',
  '/js/offline-storage.js',
  '/_framework/blazor.webassembly.js',
];

// ─── INSTALL: Cache static app shell ─────────────────────────────────────────
self.addEventListener('install', event => {
  console.log('[SW] Installing DairyFlow Service Worker...');
  event.waitUntil(
    caches.open(CACHE_VERSION)
      // Add each asset on its own so one missing/404 file can't reject the whole cache
      // (cache.addAll is atomic — a single failure would leave the app shell uncached,
      //  which is exactly what broke offline navigation before).
      .then(cache => Promise.allSettled(
        STATIC_ASSETS.map(u => cache.add(u).catch(err => console.warn('[SW] skip', u, err)))
      ))
      .then(() => self.skipWaiting())
  );
});

// ─── ACTIVATE: Clean old caches ───────────────────────────────────────────────
self.addEventListener('activate', event => {
  console.log('[SW] Activating...');
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys.filter(k => k !== CACHE_VERSION).map(k => {
          console.log('[SW] Deleting old cache:', k);
          return caches.delete(k);
        })
      )
    ).then(() => self.clients.claim())
  );
});

// ─── FETCH: Smart caching strategy ───────────────────────────────────────────
self.addEventListener('fetch', event => {
  const { request } = event;
  const url = new URL(request.url);

  // Skip non-GET requests and chrome-extension
  if (request.method !== 'GET') return;
  if (url.protocol === 'chrome-extension:') return;

  // API calls: Network-first, fall back to cached response
  if (url.pathname.startsWith(API_BASE)) {
    event.respondWith(networkFirstWithCache(request));
    return;
  }

  // Blazor framework files: Cache-first (they're hashed)
  if (url.pathname.includes('/_framework/') || url.pathname.includes('/css/')) {
    event.respondWith(cacheFirst(request));
    return;
  }

  // Navigation requests (e.g. reloading /milk): try network, else serve the cached SPA shell.
  // Blazor boots from index.html and the router renders the right page, so every in-app route
  // works offline as long as the shell + /_framework are cached.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () =>
        (await caches.match('/index.html')) ||
        (await caches.match('/')) ||
        new Response('You are offline and this page has not been cached yet.',
          { status: 503, headers: { 'Content-Type': 'text/plain' } })
      )
    );
    return;
  }

  // Default: Stale-while-revalidate
  event.respondWith(staleWhileRevalidate(request));
});

// ─── BACKGROUND SYNC: Process offline queue when back online ─────────────────
self.addEventListener('sync', event => {
  if (event.tag === 'dairyflow-sync') {
    console.log('[SW] Background sync triggered');
    event.waitUntil(processSyncQueue());
  }
});

// ─── PUSH: Handle push notifications ─────────────────────────────────────────
self.addEventListener('push', event => {
  if (!event.data) return;
  const data = event.data.json();
  event.waitUntil(
    self.registration.showNotification(data.title || 'DairyFlow', {
      body: data.message,
      icon: '/icons/icon-192.png',
      badge: '/icons/badge-72.png',
      tag: data.type,
      data: data
    })
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(
    clients.openWindow('/?notification=' + event.notification.data?.type)
  );
});

// ─── HELPERS ─────────────────────────────────────────────────────────────────

async function networkFirstWithCache(request) {
  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_VERSION);
      cache.put(request, response.clone());
    }
    return response;
  } catch (err) {
    const cached = await caches.match(request);
    if (cached) {
      console.log('[SW] Serving API from cache (offline):', request.url);
      return cached;
    }
    // Return a proper offline response for API calls
    return new Response(JSON.stringify({
      error: 'offline',
      message: 'You are currently offline. Data may not be up to date.'
    }), {
      status: 503,
      headers: { 'Content-Type': 'application/json' }
    });
  }
}

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_VERSION);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    return new Response('Offline', { status: 503 });
  }
}

async function staleWhileRevalidate(request) {
  const cache = await caches.open(CACHE_VERSION);
  const cached = await cache.match(request);

  const fetchPromise = fetch(request).then(response => {
    if (response.ok) cache.put(request, response.clone());
    return response;
  }).catch(() => null);

  return cached || await fetchPromise || new Response('Offline', { status: 503 });
}

async function processSyncQueue() {
  // This is called by the Blazor app via postMessage
  // The actual sync logic lives in the app's SyncService.cs (Blazor)
  const clients = await self.clients.matchAll({ type: 'window' });
  clients.forEach(client => {
    client.postMessage({ type: 'PROCESS_SYNC_QUEUE' });
  });
}

// ─── MESSAGE HANDLER: Communication from Blazor app ──────────────────────────
self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
  if (event.data?.type === 'SYNC_COMPLETE') {
    console.log('[SW] Sync completed successfully');
  }
  if (event.data?.type === 'CACHE_SNAPSHOT') {
    // Cache a full data snapshot for offline use
    const { url, data } = event.data;
    caches.open(CACHE_VERSION).then(cache => {
      const response = new Response(JSON.stringify(data), {
        headers: { 'Content-Type': 'application/json' }
      });
      cache.put(url, response);
    });
  }
});
