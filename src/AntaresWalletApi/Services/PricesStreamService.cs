using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Common.Log;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class PricesStreamService : StreamServiceBase<PriceUpdate>
    {
        private readonly ConcurrentDictionary<string, PriceUpdate> _prices = new ConcurrentDictionary<string, PriceUpdate>();

        public PricesStreamService(ILogFactory logFactory, bool needPing = false, TimeSpan? jobPeriod = null) : base(logFactory, needPing, jobPeriod)
        {
        }

        internal override Task WriteStreamDataAsync(StreamData<PriceUpdate> streamData, PriceUpdate data)
        {
            _prices[data.AssetPairId] = data;
            return Task.CompletedTask;
        }

        internal override Task ProcesJobAsync(List<StreamData<PriceUpdate>> streamList)
        {
            var tasks = new List<Task>();

            foreach (var stream in streamList)
            {
                var dataToSend = _prices.Values.Where(x => stream.Keys.Contains(x.AssetPairId, StringComparer.InvariantCultureIgnoreCase) || stream.Keys.Length == 0).ToList();

                foreach (var data in dataToSend)
                {
                    tasks.Add(WriteToStreamAsync(stream, data));
                }
            }

            _prices.Clear();

            return Task.WhenAll(tasks);
        }
    }
}
