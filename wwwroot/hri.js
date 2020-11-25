
var HotReloadInjector = (function () {
    const HRI_REQUEST_PATH = "/hri";

    function HRIPayload() {
    }
    HRIPayload.fromBuffer = (buf) => {
        let decoder = new TextDecoder ();
        let data = new DataView (buf);
        let len = data.getInt32(0, false);
        let bufView = new DataView(buf, 4, len);
        let dmeta = decoder.decode (bufView);
        return { dmeta: dmeta, dil: "" };
    }


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
                let payload = HRIPayload.fromBuffer (buf);
                console.log (`decoded payload as ${payload.dmeta}`);
                let evt2 = new MessageEvent(evt.type, { ...evt,  data : payload});
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