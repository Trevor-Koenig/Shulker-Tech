/**
 * SSE client — connects to /api/events.php and dispatches named events
 * to registered handlers. Automatically reconnects on failure.
 *
 * Usage:
 *   import { onEvent } from './events.js';
 *   onEvent('server-status', (data) => { ... });
 *
 * Or without modules, call window.SseClient.on() after the script loads.
 */
(function () {
    'use strict';

    const ENDPOINT        = '/api/events.php';
    const RECONNECT_DELAY = 5000; // ms before reconnect attempt

    const handlers = {};
    let source      = null;

    function connect() {
        source = new EventSource(ENDPOINT);

        source.addEventListener('error', () => {
            source.close();
            setTimeout(connect, RECONNECT_DELAY);
        });

        // Route named events to registered handlers
        source.addEventListener('message', (e) => dispatch('message', e));

        // Re-register all named listeners on each new connection
        Object.keys(handlers).forEach((event) => {
            source.addEventListener(event, (e) => {
                const data = JSON.parse(e.data);
                (handlers[event] || []).forEach((fn) => fn(data));
            });
        });
    }

    function dispatch(event, e) {
        try {
            const data = JSON.parse(e.data);
            (handlers[event] || []).forEach((fn) => fn(data));
        } catch (_) {}
    }

    /**
     * Register a handler for a named SSE event.
     * Can be called before the connection is established.
     */
    function onEvent(event, handler) {
        if (!handlers[event]) {
            handlers[event] = [];
            // Attach to existing source if already connected
            if (source) {
                source.addEventListener(event, (e) => {
                    try {
                        const data = JSON.parse(e.data);
                        (handlers[event] || []).forEach((fn) => fn(data));
                    } catch (_) {}
                });
            }
        }
        handlers[event].push(handler);
    }

    connect();

    window.SseClient = { on: onEvent };
})();
