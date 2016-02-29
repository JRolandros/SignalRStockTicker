using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using SignalRStockManagement.Features;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace SignalRStockManagement.Model
{
    public class StockTicker
    {
        //singleton instance. lazy initialisation is used not for perfomance but to ensure that the instance is threadsafe.
        private readonly static Lazy<StockTicker> _instance = new Lazy<StockTicker>(() =>
            new StockTicker(GlobalHost.ConnectionManager.GetHubContext<StockTickerHub>().Clients));

        //the ConcurrentDictionary is used instead of Dictionnary for thread safety. If you use Dictionary remind to lock the obect when making changes on it.
        private readonly ConcurrentDictionary<string, Stock> _stocks = new ConcurrentDictionary<string, Stock>();


        private readonly object _updateStockPricesLock=new object();

        //stock can go up or down by a percentage of this factor on each change
        private readonly double _rangePercent = 0.002;

        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(250);

        private readonly Random _updateOrNotRandom = new Random();

        private readonly Timer _timer;
        //this boolean is marked volatile to ensure that access to it is threadsafe
        private volatile bool _updatingStockPrices = false;     

        private StockTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            _stocks.Clear();
            var stocks = new List<Stock>
            {
                new Stock { Symbol = "MSFT", Price = 30.31m },
                new Stock { Symbol = "APPL", Price = 578.18m },
                new Stock { Symbol = "GOOG", Price = 570.30m }
            };
            stocks.ForEach(stock => _stocks.TryAdd(stock.Symbol, stock));

            _timer = new Timer(UpdateStockPrices, null, _updateInterval, _updateInterval);

        }

        public static StockTicker Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        private IHubConnectionContext<dynamic> Clients
        {
            get;
            set;
        }

        //method exposed to the server hub to be called to access client methods
        public IEnumerable<Stock> GetAllStocks()
        {
            return _stocks.Values;
        }


        private void UpdateStockPrices(object state)
        {
            lock (_updateStockPricesLock)
            {
                if (!_updatingStockPrices)
                {
                    _updatingStockPrices = true;

                    foreach (var stock in _stocks.Values)
                    {
                        if (TryUpdateStockPrice(stock))
                        {
                            BroadcastStockPrice(stock);
                        }
                    }

                    _updatingStockPrices = false;
                }
            }
        }

        private bool TryUpdateStockPrice(Stock stock)
        {
            // Randomly choose whether to update this stock or not
            var r = _updateOrNotRandom.NextDouble();
            if (r > .1)
            {
                return false;
            }

            // Update the stock price by a random factor of the range percent
            var random = new Random((int)Math.Floor(stock.Price));
            var percentChange = random.NextDouble() * _rangePercent;
            var pos = random.NextDouble() > .51;
            var change = Math.Round(stock.Price * (decimal)percentChange, 2);
            change = pos ? change : -change;

            stock.Price += change;
            return true;
        }

        private void BroadcastStockPrice(Stock stock)
        {
            //server calling a client method "updateStockPrice(stock)" to update data in the user interface in real time
            Clients.All.updateStockPrice(stock);
        }

    }
}