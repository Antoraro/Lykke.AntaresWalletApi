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

        internal override void WriteStreamData(StreamData<PriceUpdate> streamData, PriceUpdate data)
        {
            _prices[data.AssetPairId] = data;
        }

        internal override Task ProcesJobAsync(List<StreamData<PriceUpdate>> streamList)
        {
            foreach (var stream in streamList)
            {
                var dataToSend = _prices.Values.Where(x => stream.Keys.Contains(x.AssetPairId)).ToList();

                foreach (var data in dataToSend)
                {
                    WriteToStream(stream, data);
                }
            }

            return Task.CompletedTask;
        }
    }
}
