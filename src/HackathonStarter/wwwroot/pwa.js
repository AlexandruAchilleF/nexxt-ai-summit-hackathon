// Registers the service worker and powers the "Install app" button (Chrome/Edge on desktop and Android).
// iPhone has no install prompt: Share → Add to Home Screen.
(() => {
  if ('serviceWorker' in navigator) navigator.serviceWorker.register('/service-worker.js').catch(() => { });

  let deferred = null;
  addEventListener('beforeinstallprompt', e => {
    e.preventDefault();
    deferred = e;
    document.documentElement.classList.add('pwa-installable');
  });
  addEventListener('appinstalled', () => {
    deferred = null;
    document.documentElement.classList.remove('pwa-installable');
  });
  // Delegated, so it survives Blazor re-rendering the button.
  document.addEventListener('click', async e => {
    if (!deferred || !e.target.closest('.pwa-install')) return;
    deferred.prompt();
    await deferred.userChoice;
    deferred = null;
    document.documentElement.classList.remove('pwa-installable');
  });
})();
