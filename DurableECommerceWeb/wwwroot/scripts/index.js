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
                description: 'Make things go faster',
                image: 'images/strat.jpg'
            },
            {
                id: 'achieving-your-goals',
                name: 'Achieving Your Goals',
                price: 400,
                description: 'Make your dreams come true',
                image: 'images/football.jpg'
            },
            {
                id: 'cake-driven-development',
                name: 'Cake Driven Development',
                price: 50,
                description: 'Make your code tastier',
                image: 'images/cakes.jpg'
            }
        ],
        orderId: null,
        cart: [],
        email: 'durable-funcs-customer@mailinator.com'
    },
    methods: {
        onOrderCreated: function (orderInfo) {
            $('#shoppingCart').modal('hide');
            this.orderId = orderInfo.id;
            this.cart.length = 0;
        },
        addToCart: function(product) {
            if (this.cart.indexOf(product) === -1)
                this.cart.push(product);
        },
        removeFromCart: function (product) {
            var index = this.cart.indexOf(product);
            if (index > -1) {
                this.cart.splice(index, 1);
            }
        },
        buy: function () {
            var items = [];
            for (var i = 0; i < this.cart.length; i++) {
                items.push({
                    ProductId: this.cart[i].id,
                    Amount: this.cart[i].price
                });
            }
            postData('/api/CreateOrder',
                {
                    Items: items,
                    PurchaserEmail: this.email
                })
                .then(data => this.onOrderCreated(data))
                .catch(error => console.error(error));
        }
    }
});
