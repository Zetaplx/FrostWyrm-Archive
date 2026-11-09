self.importScripts('./service-worker-assets.js');
const prefix = 'cryptid-offline-';
const cacheName = prefix + self.assetsManifest.version;
const scope = new URL('./', self.registration.scope);
const assets = self.assetsManifest.assets.filter(a => !/\.(br|gz|pdb)$/.test(a.url) && !/service-worker/.test(a.url));
self.addEventListener('install', event => event.waitUntil((async () => {
    const cache = await caches.open(cacheName);
    await cache.addAll(assets.map(a => new Request(new URL(a.url, scope), { integrity: a.hash, cache: 'no-cache' })));
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
    for (const name of await caches.keys()) if (name.startsWith(prefix) && name !== cacheName) await caches.delete(name);
    await self.clients.claim();
})()));
self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET' || new URL(event.request.url).origin !== scope.origin) return;
    event.respondWith((async () => {
        const cache = await caches.open(cacheName);
        const target = event.request.mode === 'navigate' ? new URL('index.html', scope).href : event.request;
        return await cache.match(target) || fetch(event.request);
    })());
});
