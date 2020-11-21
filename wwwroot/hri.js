
var HotReloadInjector = (function () {
    const HRI_REQUEST_PATH = "/hri";
    class HotReloadInjector {
        _loc;
        constructor(loc) {
            this._loc = loc;
        }

        start_ws (listener) {
            let ws = new WebSocket (this._loc);
            ws.addEventListener('open', listener.onopen);
            ws.addEventListener('close', listener.onclose);
            ws.addEventListener('message', listener.onmessage);
        }

        static getHotReloadUriFromDocumentUri(loc) {
            let proto = loc.protocol === 'https:' ? "wss" : "ws";
            let port = loc.port ? (':' + loc.port) : '';
            let host = loc.hostname;
            return proto + "://" + host + port + HRI_REQUEST_PATH;
        }
    }

    return HotReloadInjector;
})();