// get a query string parameter https://stackoverflow.com/a/901144/7532
function getParameterByName(name, url) {
    if (!url) url = window.location.href;
    name = name.replace(/[\[\]]/g, '\\$&');
    var regex = new RegExp('[?&]' + name + '(=([^&#]*)|&|#|$)'),
        results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return '';
    return decodeURIComponent(results[2].replace(/\+/g, ' '));
}

function getOrderId() {
    var id = getParameterByName('id');
    if (id) return id;
    var pathArray = window.location.pathname.split('/');
    return pathArray[pathArray.length - 1];
}

Vue.filter('formatDateTime',
    function (value) {
        var d = new Date(value);
        return d.toLocaleString();
    });

Vue.filter('formatRuntimeStatus',
    function (value) {
        return ["Running", "Completed", "ContinuedAsNew", "Failed", "Canceled", "Terminated", "Pending"][value];
    });

Vue.component('order-status',
    {
        props: ['orderStatus'],
        template: '#orderStatusTemplate'
    });

var postData = (url, data) =>
    fetch(url,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json; charset=utf-8"
            },
            body: JSON.stringify(data)
        });

var app = new Vue({
    el: '#app',
    data: {
        orderId: null,
        orderStatus: null,
        errorMessage: null
    },
    mounted: function () {
        this.orderId = getOrderId();
        this.checkStatus();
    },

    methods: {
        checkStatus: function (event) {
            if (event) event.preventDefault();
            this.errorMessage = null;
            this.orderStatus = null;
            fetch(`/api/orderstatus/${this.orderId}`)
                .then(response => {
                    if (response.status === 404) {
                        this.errorMessage = `Order ${this.orderId} not found`;
                    } else {
                        response.json().then(json => this.orderStatus = json);
                    }
                }
                )
                .catch(err => console.error(err));
        }
    }
});