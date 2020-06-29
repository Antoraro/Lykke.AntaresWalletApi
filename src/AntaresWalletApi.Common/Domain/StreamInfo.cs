using System;
using System.Threading;
using Grpc.Core;

namespace AntaresWalletApi.Common.Domain
{
    public class StreamInfo<T>
    {
        public IServerStreamWriter<T> Stream { get; set; }
        public CancellationToken? CancelationToken { get; set; }
        public string[] Keys { get; set; } = Array.Empty<string>();
        public string Peer { get; set; }
    }
}
