const postData = (url, data) => {
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
        const d = new Date(value);
        return d.toLocaleDateString();
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
        }
    }
});
