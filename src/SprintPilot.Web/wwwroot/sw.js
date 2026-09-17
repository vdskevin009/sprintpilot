// Deliberately no data or navigation cache: Blazor Server and Azure DevOps need a connection.
self.addEventListener('install',event=>self.skipWaiting());
self.addEventListener('activate',event=>event.waitUntil(self.clients.claim()));
self.addEventListener('fetch',()=>{});
