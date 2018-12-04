var postData = (url, data) => {
    return fetch(url,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json; charset=utf-8"
            },
            body: JSON.stringify(data)
        });
};

Vue.filter('formatDate',
    function (value) {
        var d = new Date(value);
        return d.toLocaleDateString();
    });

Vue.filter('formatOrderItems',
    function (value) {
        var x = "";
        for (var i = 0; i < value.length; i++) {
            x += value[i].ProductId + ", ";
        }
        return x.substring(0,x.length-2);
    });

Vue.filter('formatRuntimeStatus',
    function (value) {
        return ["Running",
            "Completed",
            "ContinuedAsNew",
            "Failed",
            "Canceled",
            "Terminated",
            "Pending"][value];
    });

var app = new Vue({
    el: '#app',
    data: {
        orders: null,
        errorMessage: null
    },
    mounted: function () {
        this.getOrderStatuses();
    },
    methods: {
        getOrderStatuses: function () {
            this.errorMessage = null;
            fetch('/api/getallorders/')
                .then(response => response.json())
                .then(json => {
                    this.orders = json;
                })
                .catch(err => {
                    this.errorMessage = `failed to get orders (${err})`;
                });
        },
        approve: function (order, status) {
            postData(`/api/approve/${order.input.Id}`, status)
                .then(_ => order.customStatus = '');
        },
        deleteOrder: function (order) {
            fetch(`/api/order/${order.input.Id}`, { method: 'DELETE' })
                .then(_ => {
                    var index = this.orders.indexOf(order);
                    if (index > -1) {
                        this.orders.splice(index, 1);
                    }
                })
                .catch(err => {
                this.errorMessage = `failed to delete order (${err})`;
            });
        }
    }
});
