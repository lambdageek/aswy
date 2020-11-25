
var HotReloadInjector = (function () {
    const HRI_REQUEST_PATH = "/hri";
    const READY_STATE_OPEN = 1;

    function getHotReloadUriFromDocumentUri(loc) {
        let proto = loc.protocol === 'https:' ? "wss" : "ws";
        let port = loc.port ? (':' + loc.port) : '';
        let host = loc.hostname;
        return proto + "://" + host + port + HRI_REQUEST_PATH;
    }

    /// An HRIPayload is an object with 
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
        _ws;
        _uri;
        constructor(uri) {
            this._uri = uri;
        }

        start_ws (listener) {
            let cb = { onopen: null, onclose: null, onmessage: null };
            if (listener !== undefined && listener !== null) {
                if (listener.onopen !== undefined)
                    cb.onopen = listener.onopen;                
                if (listener.onclose !== undefined)
                    cb.onclose = listener.onclose;
                if (listener.onmessage !== undefined)
                    cb.onmessage = listener.onmessage;                    
            }
            this._ws = new WebSocket (this._uri);
            this._ws.addEventListener('open', this.#onopen.bind (this, cb.onopen));
            this._ws.addEventListener('close', this.#onclose.bind(this, cb.onclose));
            this._ws.addEventListener('error', this.#onerror.bind(this));
            this._ws.addEventListener('message', this.#onmessage.bind(this, cb.onmessage));
        }

        #onerror (evt) {
            console.log ("HotReloadInjector closed due to error, error: ", evt);
            this._ws = undefined;
        }

        #onopen (onopen, evt) {
            console.log ("HotReloadInjector websocket open");
            if (onopen)
                onopen (evt);
        }

        #onclose (onclose, evt) {
            console.log ("HotReloadInjector websocket closed: ", evt );
            this._ws = undefined;
            if (onclose)
                onclose (evt);
        }

        #onmessage (onmessage, evt) {
            console.log ("got a payload, decoding...")
            evt.data.arrayBuffer().then (buf => {
                let payload = HRIPayload.fromBuffer (buf);
                console.log (`decoded payload as ${payload.dmeta}`);
                let evt2 = new MessageEvent(evt.type, { ...evt,  data : payload});
                if (onmessage)
                    onmessage (evt2);
            });

        }

        

        static create(options) {
            let start = true;
            let callbacks = undefined;
            if (options !== undefined && options !== null) {
                if (options.start !== undefined && !options.start)
                    start = false;
                if (options.callbacks !== undefined && options.callbacks !== null)
                    callbacks = options.callbacks;
            }
            let hri = new HotReloadInjector(getHotReloadUriFromDocumentUri (document.location));
            if (start)
                hri.start_ws(callbacks);
            return hri;
        }
    }

    function hriHtml () {
        let body = document.getElementsByTagName ('body') [0];
        let box = document.createElement('div');
        box.style.cssText = `
        position: absolute;
        z-index: 1;
        width: 20%;
        height: 20%;
        right: 0px;
        top: 0px;
        border: 2px solid black;
        padding: 0;
        margin: 0;
        background: rgba(240,240,240,0.7);
        `;
        let button = document.createElement('button');
        button.innerText = "connect";
        box.appendChild (button);
        let textarea = document.createElement('div');
        textarea.style.cssText = `
        background: rgba(192,192,192,0.9);
        overflow: scroll;
        padding: 0;
        margin: 0;
        white-space: pre-line;
        `;
        textarea.innerText = "hot reload injector\n";
        box.appendChild (textarea);

        button.addEventListener('click', () => {
            button.disabled = true;
            let callbacks = {
                onmessage: (evt) => {
                    textarea.appendChild(document.createTextNode("payload is " + evt.data.dmeta + "\n"));
                },
            };
            HotReloadInjector.create ({callbacks: callbacks});
        })

        body.appendChild (box);        
    }

    document.addEventListener ('DOMContentLoaded', () => { console.log ("injecting"); hriHtml (); });
    return HotReloadInjector;
})();