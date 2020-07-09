using System;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Common.Domain.Services;
using Autofac;
using AutoMapper;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class ApplicationManager : IStartable, IDisposable
    {
        private readonly MyNoSqlTcpClient _noSqlTcpClient;
        private readonly IMyNoSqlServerDataReader<PriceEntity> _pricesReader;
        private readonly IMyNoSqlServerDataReader<CandleEntity> _candlesReader;
        private readonly IStreamService<PriceUpdate> _priceStream;
        private readonly IStreamService<CandleUpdate> _candlesStream;
        private readonly IMapper _mapper;

        public ApplicationManager(
            MyNoSqlTcpClient noSqlTcpClient,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            IMyNoSqlServerDataReader<CandleEntity> candlesReader,
            IStreamService<PriceUpdate> priceStream,
            IStreamService<CandleUpdate> candlesStream,
            IMapper mapper
            )
        {
            _noSqlTcpClient = noSqlTcpClient;
            _pricesReader = pricesReader;
            _candlesReader = candlesReader;
            _priceStream = priceStream;
            _candlesStream = candlesStream;
            _mapper = mapper;
        }

        public void Start()
        {
            _pricesReader.SubscribeToChanges(prices =>
            {
                foreach (var price in prices)
                {
                    _priceStream.WriteToStream(_mapper.Map<PriceUpdate>(price), price.AssetPairId);
                }
            });

            _candlesReader.SubscribeToChanges(candles =>
            {
                foreach (var candle in candles)
                {
                    var key = $"{candle.AssetPairId}_{candle.PriceType}_{candle.TimeInterval}";
                    _candlesStream.WriteToStream(_mapper.Map<CandleUpdate>(candle), key);
                }
            });

            Console.WriteLine("Stream services started.");
        }

        public void Dispose()
        {
            _priceStream.Stop();
            _noSqlTcpClient.Stop();
            Console.WriteLine("Stream services stopped.");
        }
    }
}
