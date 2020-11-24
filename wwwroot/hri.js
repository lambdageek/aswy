
var HotReloadInjector = (function () {
    const HRI_REQUEST_PATH = "/hri";
    class HotReloadInjector {
        _loc;
        _decoder;
        constructor(loc) {
            this._loc = loc;
            this._decoder = new TextDecoder();
        }

        start_ws (listener) {
            let ws = new WebSocket (this._loc);
            ws.addEventListener('open', this.#onopen.bind (this, listener.onopen));
            ws.addEventListener('close', listener.onclose);
            ws.addEventListener('message', this.#onmessage.bind(this, listener.onmessage));
        }

        #onopen (onopen, evt) {
            console.log ("hot reload injector websocket open");
            onopen (evt);
        }

        #onmessage (onmessage, evt) {
            console.log ("got a payload, decoding...")
            evt.data.arrayBuffer().then (buf => {
                let bufView = new Uint8Array(buf, 4); // skip length
                let data2 = this._decoder.decode (bufView);
                console.log (`decoded payload as ${data2}`);
                let evt2 = new MessageEvent(evt.type, { ...evt,  data : data2});
                onmessage (evt2);
            });

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