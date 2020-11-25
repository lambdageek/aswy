
(function () {
    'use strict'

    

    class MyApp {
        constructor() {

            this.bingo = null;
            this.clicky = null;
            this.clicks = 0;
            this.n = 0;
        }

        onclick(_evt) {
            this.bingo.innerText = 'ping';
            this.clicky.innerText = `clicked ${++this.clicks} times`;
        }

        

        ready(_event) {
            this.bingo = document.getElementById("bingo");
            this.bingo.innerText = "ready to go";
            this.clicky = document.getElementById("clicky");

            this.clicky.addEventListener('click', this.onclick.bind(this));
        }
    }




    let app = new MyApp ();

    window.addEventListener('DOMContentLoaded', app.ready.bind(app));

})();
