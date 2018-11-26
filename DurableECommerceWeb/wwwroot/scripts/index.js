var postData = function (url, data) {
    return fetch(url,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json; charset=utf-8"
            },
            body: JSON.stringify(data)
        })
        .then(response => response.json());
};

var app = new Vue({
    el: '#app',
    data: {
        products: [
            {
                id: 'performance-tuning',
                name: 'Performance Tuning',
                price: 2000,
                image: 'images/strat.jpg'
            },
            {
                id: 'achieving-your-goals',
                name: 'Achieving Your Goals',
                price: 400,
                image: 'images/football.jpg'
            },
            {
                id: 'cake-driven-development',
                name: 'Cake Driven Development',
                price: 50,
                image: 'images/cakes.jpg'
            }
        ],
        orderId: null,
        orderedProduct: ""
    },
    methods: {
        onOrderCreated: function (orderInfo) {
            this.orderId = orderInfo.id;
        },
        buy: function (product) {
            this.orderedProduct = product;
            //console.log(`purchased a ${product.id} for ${product.price}`);
            postData('/api/CreateOrder',
                {
                    ProductId: product.id,
                    Amount: product.price,
                    PurchaserEmail: 'durable-funcs-customer@mailinator.com'
                })
                .then(data => this.onOrderCreated(data))
                .catch(error => console.error(error));
        }
    }
});
