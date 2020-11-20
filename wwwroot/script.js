
(function () {
    'use strict'

    function start_ws (base_url, listener) {
        let ws = new WebSocket (base_url + "/ws");
        ws.addEventListener('open', listener.onopen);
        ws.addEventListener('close', listener.onclose);
        ws.addEventListener('message', listener.onmessage);
    }

    class MyApp {
        constructor() {

            this.bingo = null;
            this.clicky = null;
            this.n = 0;
            this.base_ws = null;
        }

        onclick(_evt) {
            this.bingo.innerText = `clicked ${++this.n} times`;
            this.clicky.innerText = "click me again";
            this.clicky.disabled = true;
            start_ws (this.base_ws, {
                onopen: (_evt) => { this.bingo.innerText += " and connection opened"},
                onclose: (_evt) => {
                    this.clicky.disabled = false;
                    this.bingo.innerText += " and connection closed"
                },
                onmessage: (evt) => { this.bingo.innerText = `got ${++this.n}: ${evt.data}`}
            })            
        }

        bindProtocolStuff() {
            let proto = document.location.protocol === 'https:' ? "wss" : "ws";
            let port = document.location.port ? (':' + document.location.port) : '';
            let host = document.location.hostname;
            this.base_ws = proto + "://" + host + port;
        }

        ready(_event) {
            this.   bindProtocolStuff ();
            this.bingo = document.getElementById("bingo");
            this.bingo.innerText = "ready to go";
            this.clicky = document.getElementById("clicky");

            this.clicky.addEventListener('click', this.onclick.bind(this));
        }
    }




    let app = new MyApp ();

    window.addEventListener('DOMContentLoaded', app.ready.bind(app));

})();
