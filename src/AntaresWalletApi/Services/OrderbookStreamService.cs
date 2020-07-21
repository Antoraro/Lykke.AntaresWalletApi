using System;
using Google.Protobuf.WellKnownTypes;
using Lykke.Common.Log;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class OrderbookStreamService : StreamServiceBase<Orderbook>
    {
        private readonly OrderbooksService _orderbooksService;

        public OrderbookStreamService(OrderbooksService orderbooksService,
            ILogFactory logFactory,
            bool needPing = false) : base(logFactory, needPing)
        {
            _orderbooksService = orderbooksService;
        }

        internal override Orderbook ProcessDataBeforeSend(Orderbook data, StreamData<Orderbook> streamData)
        {
            return GetOrderbook(data, streamData, false);
        }

        internal override Orderbook ProcessPingDataBeforeSend(Orderbook data, StreamData<Orderbook> streamData)
        {
            return GetOrderbook(data, streamData, true);
        }

        private Orderbook GetOrderbook(Orderbook data, StreamData<Orderbook> streamData, bool updateDate)
        {
            if (streamData.LastSentData == null)
                return data;

            Orderbook update = _orderbooksService.GetOrderbookUpdates(streamData.LastSentData, data);

            if (updateDate)
                update.Timestamp = Timestamp.FromDateTime(DateTime.UtcNow);

            return update;

        }
    }
}
