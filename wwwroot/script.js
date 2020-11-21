
(function () {
    'use strict'

    

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
            let hri = new HotReloadInjector(this.base_ws);
            hri.start_ws ({
                onopen: (_evt) => { this.bingo.innerText += " and connection opened"},
                onclose: (_evt) => {
                    this.clicky.disabled = false;
                    this.bingo.innerText += " and connection closed"
                },
                onmessage: (evt) => { this.bingo.innerText = `got ${++this.n}: ${evt.data}`}
            });
        }

        

        ready(_event) {
            this.base_ws = HotReloadInjector.getHotReloadUriFromDocumentUri (document.location);
            this.bingo = document.getElementById("bingo");
            this.bingo.innerText = "ready to go";
            this.clicky = document.getElementById("clicky");

            this.clicky.addEventListener('click', this.onclick.bind(this));
        }
    }




    let app = new MyApp ();

    window.addEventListener('DOMContentLoaded', app.ready.bind(app));

})();
