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
        private readonly IStreamService<PriceUpdate> _priceStream;
        private readonly IMapper _mapper;

        public ApplicationManager(
            MyNoSqlTcpClient noSqlTcpClient,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            IStreamService<PriceUpdate> priceStream,
            IMapper mapper
            )
        {
            _noSqlTcpClient = noSqlTcpClient;
            _pricesReader = pricesReader;
            _priceStream = priceStream;
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
