// Install support for the PWA. Loaded before Blazor so the browser's install event is not missed.
(function () {
    const listeners = new Set();
    let deferred = null;
    const dismissKey = 'ordali.install.dismissed';

    const isIos = () => /iPhone|iPad|iPod/i.test(navigator.userAgent)
        || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent));
    const notify = () => listeners.forEach(l => l.invokeMethodAsync('OnInstallStateChanged'));

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        deferred = event;
        notify();
    });
    window.addEventListener('appinstalled', () => {
        deferred = null;
        notify();
    });

    window.ordaliInstall = {
        state() {
            let dismissed = false;
            try { dismissed = sessionStorage.getItem(dismissKey) === '1'; } catch { }
            return {
                standalone: matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
                mobile: isIos() || /Android|Mobi/i.test(navigator.userAgent),
                ios: isIos(),
                canPrompt: deferred !== null,
                dismissed,
            };
        },
        async prompt() {
            if (!deferred) {
                return 'unavailable';
            }
            const event = deferred;
            deferred = null;
            await event.prompt();
            const choice = await event.userChoice;
            notify();
            return choice.outcome;
        },
        // Hidden only for this visit, so the next visit from the browser asks again.
        dismiss() {
            try { sessionStorage.setItem(dismissKey, '1'); } catch { }
        },
        subscribe(listener) { listeners.add(listener); },
        unsubscribe(listener) { listeners.delete(listener); },
    };
})();
