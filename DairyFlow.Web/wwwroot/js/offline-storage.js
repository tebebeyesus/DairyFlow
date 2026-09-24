// OfflineStorage.js — IndexedDB wrapper for DairyFlow Blazor WASM
// Called from Blazor via JS Interop

const DB_NAME = 'DairyFlowDB';
const DB_VERSION = 3;

const STORES = {
  cows:           'cows',
  milkLogs:       'milkLogs',
  sales:          'sales',
  expenses:       'expenses',
  healthRecords:  'healthRecords',
  feeds:          'feeds',
  feedLogs:       'feedLogs',
  notifications:  'notifications',
  syncQueue:      'syncQueue',
  meta:           'meta'
};

let db = null;

// ─── OPEN DATABASE ────────────────────────────────────────────────────────────
async function openDB() {
  if (db) return db;
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);

    req.onupgradeneeded = (event) => {
      const database = event.target.result;

      // Cows store
      if (!database.objectStoreNames.contains(STORES.cows)) {
        const cs = database.createObjectStore(STORES.cows, { keyPath: 'id' });
        cs.createIndex('status', 'status');
        cs.createIndex('farmId', 'farmId');
      }

      // Milk logs
      if (!database.objectStoreNames.contains(STORES.milkLogs)) {
        const ms = database.createObjectStore(STORES.milkLogs, { keyPath: 'id' });
        ms.createIndex('logDate', 'logDate');
        ms.createIndex('cowId', 'cowId');
        ms.createIndex('farmId', 'farmId');
      }

      // Sales
      if (!database.objectStoreNames.contains(STORES.sales)) {
        const ss = database.createObjectStore(STORES.sales, { keyPath: 'id' });
        ss.createIndex('saleDate', 'saleDate');
      }

      // Expenses
      if (!database.objectStoreNames.contains(STORES.expenses)) {
        const es = database.createObjectStore(STORES.expenses, { keyPath: 'id' });
        es.createIndex('expenseDate', 'expenseDate');
        es.createIndex('category', 'category');
      }

      // Health records
      if (!database.objectStoreNames.contains(STORES.healthRecords)) {
        const hs = database.createObjectStore(STORES.healthRecords, { keyPath: 'id' });
        hs.createIndex('cowId', 'cowId');
        hs.createIndex('recordDate', 'recordDate');
      }

      // Feeds (inventory — cached for offline read; edited online)
      if (!database.objectStoreNames.contains(STORES.feeds)) {
        database.createObjectStore(STORES.feeds, { keyPath: 'id' });
      }

      // Feed logs (daily feeding per cow)
      if (!database.objectStoreNames.contains(STORES.feedLogs)) {
        const fl = database.createObjectStore(STORES.feedLogs, { keyPath: 'id' });
        fl.createIndex('logDate', 'logDate');
        fl.createIndex('cowId', 'cowId');
        fl.createIndex('feedId', 'feedId');
      }

      // Sync queue (offline changes waiting to be pushed to server)
      if (!database.objectStoreNames.contains(STORES.syncQueue)) {
        const sq = database.createObjectStore(STORES.syncQueue, { keyPath: 'localId' });
        sq.createIndex('synced', 'synced');
        sq.createIndex('timestamp', 'timestamp');
      }

      // Notifications
      if (!database.objectStoreNames.contains(STORES.notifications)) {
        database.createObjectStore(STORES.notifications, { keyPath: 'id' });
      }

      // Meta (last sync time, user info, etc.)
      if (!database.objectStoreNames.contains(STORES.meta)) {
        database.createObjectStore(STORES.meta, { keyPath: 'key' });
      }
    };

    req.onsuccess = () => { db = req.result; resolve(db); };
    req.onerror = () => reject(req.error);
  });
}

// ─── GENERIC CRUD ─────────────────────────────────────────────────────────────
async function getAll(storeName) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readonly');
    const req = tx.objectStore(storeName).getAll();
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function getById(storeName, id) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readonly');
    const req = tx.objectStore(storeName).get(id);
    req.onsuccess = () => resolve(req.result || null);
    req.onerror = () => reject(req.error);
  });
}

async function upsert(storeName, item) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readwrite');
    const req = tx.objectStore(storeName).put(item);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function bulkUpsert(storeName, items) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readwrite');
    const store = tx.objectStore(storeName);
    items.forEach(item => store.put(item));
    tx.oncomplete = () => resolve(items.length);
    tx.onerror = () => reject(tx.error);
  });
}

async function remove(storeName, id) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readwrite');
    const req = tx.objectStore(storeName).delete(id);
    req.onsuccess = () => resolve(true);
    req.onerror = () => reject(req.error);
  });
}

async function clearStore(storeName) {
  const database = await openDB();
  return new Promise((resolve, reject) => {
    const tx = database.transaction(storeName, 'readwrite');
    const req = tx.objectStore(storeName).clear();
    req.onsuccess = () => resolve(true);
    req.onerror = () => reject(req.error);
  });
}

// ─── META ─────────────────────────────────────────────────────────────────────
async function getMeta(key) {
  const item = await getById(STORES.meta, key);
  return item?.value ?? null;
}

async function setMeta(key, value) {
  await upsert(STORES.meta, { key, value, updatedAt: new Date().toISOString() });
}

// ─── SYNC QUEUE ───────────────────────────────────────────────────────────────
async function addToSyncQueue(operation) {
  const entry = {
    localId: crypto.randomUUID(),
    ...operation,
    synced: false,
    retryCount: 0,
    timestamp: new Date().toISOString()
  };
  await upsert(STORES.syncQueue, entry);
  return entry.localId;
}

async function getPendingSyncItems() {
  const all = await getAll(STORES.syncQueue);
  return all.filter(item => !item.synced).sort((a, b) => a.timestamp.localeCompare(b.timestamp));
}

async function markSyncItemComplete(localId, serverId) {
  const item = await getById(STORES.syncQueue, localId);
  if (item) {
    item.synced = true;
    item.serverId = serverId;
    item.syncedAt = new Date().toISOString();
    await upsert(STORES.syncQueue, item);
  }
}

async function markSyncItemFailed(localId, error) {
  const item = await getById(STORES.syncQueue, localId);
  if (item) {
    item.retryCount = (item.retryCount || 0) + 1;
    item.lastError = error;
    item.lastRetry = new Date().toISOString();
    await upsert(STORES.syncQueue, item);
  }
}

async function getSyncQueueCount() {
  const pending = await getPendingSyncItems();
  return pending.length;
}

// ─── SNAPSHOT (full data load for offline) ───────────────────────────────────
async function loadSnapshot(snapshot) {
  const tasks = [];
  if (snapshot.cows)          tasks.push(bulkUpsert(STORES.cows, snapshot.cows));
  if (snapshot.milkLogs)      tasks.push(bulkUpsert(STORES.milkLogs, snapshot.milkLogs));
  if (snapshot.sales)         tasks.push(bulkUpsert(STORES.sales, snapshot.sales));
  if (snapshot.expenses)      tasks.push(bulkUpsert(STORES.expenses, snapshot.expenses));
  if (snapshot.healthRecords) tasks.push(bulkUpsert(STORES.healthRecords, snapshot.healthRecords));
  if (snapshot.feeds)         tasks.push(bulkUpsert(STORES.feeds, snapshot.feeds));
  if (snapshot.feedLogs)      tasks.push(bulkUpsert(STORES.feedLogs, snapshot.feedLogs));
  await Promise.all(tasks);
  await setMeta('lastSnapshotAt', new Date().toISOString());
  return true;
}

// ─── BLAZOR INTEROP EXPORTS ───────────────────────────────────────────────────
window.DairyFlowDB = {
  // Cows
  getAllCows:            () => getAll(STORES.cows),
  getCow:               (id) => getById(STORES.cows, id),
  saveCow:              (cow) => upsert(STORES.cows, cow),
  deleteCow:            (id) => remove(STORES.cows, id),

  // Milk
  getAllMilkLogs:        () => getAll(STORES.milkLogs),
  saveMilkLog:          (log) => upsert(STORES.milkLogs, log),
  deleteMilkLog:        (id) => remove(STORES.milkLogs, id),

  // Sales
  getAllSales:           () => getAll(STORES.sales),
  saveSale:             (sale) => upsert(STORES.sales, sale),
  deleteSale:           (id) => remove(STORES.sales, id),

  // Expenses
  getAllExpenses:        () => getAll(STORES.expenses),
  saveExpense:          (exp) => upsert(STORES.expenses, exp),
  deleteExpense:        (id) => remove(STORES.expenses, id),

  // Health
  getAllHealthRecords:   () => getAll(STORES.healthRecords),
  saveHealthRecord:     (hr) => upsert(STORES.healthRecords, hr),
  deleteHealthRecord:   (id) => remove(STORES.healthRecords, id),

  // Feeds (read cache)
  getAllFeeds:           () => getAll(STORES.feeds),
  saveFeed:             (f) => upsert(STORES.feeds, f),
  deleteFeed:           (id) => remove(STORES.feeds, id),

  // Feed logs
  getAllFeedLogs:        () => getAll(STORES.feedLogs),
  saveFeedLog:          (fl) => upsert(STORES.feedLogs, fl),
  deleteFeedLog:        (id) => remove(STORES.feedLogs, id),

  // Sync queue
  addToSyncQueue,
  getPendingSyncItems,
  markSyncItemComplete,
  markSyncItemFailed,
  getSyncQueueCount,

  // Meta
  getMeta,
  setMeta,

  // Snapshot
  loadSnapshot,
  clearAllData: async () => {
    await Promise.all(Object.values(STORES).map(s => clearStore(s)));
    return true;
  }
};

// ─── CONNECTIVITY BRIDGE (Blazor SyncService) ────────────────────────────────
// Blazor's SyncService passes a DotNetObjectReference here so the browser's
// online/offline events call its instance methods directly.
window.DairyFlowSync = {
  registerConnectivity: (dotNetRef) => {
    window.addEventListener('dairyflow:online',  () => dotNetRef.invokeMethodAsync('OnOnline'));
    window.addEventListener('dairyflow:offline', () => dotNetRef.invokeMethodAsync('OnOffline'));
  },
  isOnline: () => navigator.onLine
};

// ─── ONLINE/OFFLINE EVENTS ───────────────────────────────────────────────────
window.addEventListener('online', () => {
  window.dispatchEvent(new CustomEvent('dairyflow:online'));
  // Trigger background sync registration
  if ('serviceWorker' in navigator && 'SyncManager' in window) {
    navigator.serviceWorker.ready.then(sw => sw.sync.register('dairyflow-sync'));
  }
});

window.addEventListener('offline', () => {
  window.dispatchEvent(new CustomEvent('dairyflow:offline'));
});

// Register service worker
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('/service-worker.js')
    .then(reg => console.log('[DairyFlow] Service Worker registered', reg.scope))
    .catch(err => console.warn('[DairyFlow] SW registration failed', err));
}

console.log('[DairyFlow] Offline storage initialized');
