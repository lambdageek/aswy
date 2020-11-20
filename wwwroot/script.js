
(function () {
    'use strict'

    class MyApp {
        constructor() {

            this.bingo = null;
            this.clicky = null;
            this.n = 0;

        }
        onclick(button, evt) {
            this.bingo.innerText = `clicked ${++this.n} times`;
            this.clicky.innerText = "click me again";
        }
        ready(event) {
            this.bingo = document.getElementById("bingo");
            this.bingo.innerText = "ready to go";
            this.clicky = document.getElementById("clicky");

            this.clicky.addEventListener('click', this.onclick.bind(this));
        }
    }




    let app = new MyApp ();

    window.addEventListener('DOMContentLoaded', app.ready.bind(app));

})();
