using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntaresWalletApi.Common.Domain.Services
{
    public interface IStreamService<T> : IDisposable where T : class
    {
        Task RegisterStream(StreamInfo<T> streamInfo, List<T> initData = null);
        void WriteToStream(T data, string key = null);
        void Stop();
    }
}
