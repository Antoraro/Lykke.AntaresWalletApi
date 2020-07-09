using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Common.Domain.Services;
using Common;
using Lykke.Common.Log;

namespace AntaresWalletApi.Services
{
    public class StreamService<T>: IStreamService<T> where T : class
    {
        private readonly List<StreamData<T>> _streamList = new List<StreamData<T>>();
        private readonly TimerTrigger _checkTimer;
        private readonly TimerTrigger _pingTimer;

        public StreamService(ILogFactory logFactory, bool needPing = false)
        {
            _checkTimer = new TimerTrigger(nameof(StreamService<T>), TimeSpan.FromSeconds(10), logFactory);
            _checkTimer.Triggered += CheckStreams;
            _checkTimer.Start();

            if (needPing)
            {
                _pingTimer = new TimerTrigger(nameof(StreamService<T>), TimeSpan.FromSeconds(30), logFactory);
                _pingTimer.Triggered += Ping;
                _pingTimer.Start();
            }
        }

        public void WriteToStream(T data, string key = null)
        {
            var items = string.IsNullOrEmpty(key)
                ? _streamList.ToArray()
                : _streamList.Where(x => x.Keys.Contains(key, StringComparer.InvariantCultureIgnoreCase) || x.Keys.Length == 0).ToArray();

            items = items.Where(x => !x.CancelationToken?.IsCancellationRequested ?? true).ToArray();
            foreach (var streamData in items)
            {
                streamData.LastSentData = streamData.KeepLastData ? data : null;
                streamData.Stream.WriteAsync(data)
                    .ContinueWith(t => RemoveStream(streamData), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public Task RegisterStream(StreamInfo<T> streamInfo, List<T> initData = null)
        {
            var data = StreamData<T>.Create(streamInfo, initData);

            _streamList.Add(data);

            if (initData == null)
                return data.CompletionTask.Task;

            foreach (var value in initData)
            {
                data.Stream.WriteAsync(value);
            }

            return data.CompletionTask.Task;
        }

        public void Dispose()
        {
            foreach (var streamInfo in _streamList)
            {
                streamInfo.CompletionTask.TrySetResult(1);
                Console.WriteLine($"Remove stream connect (peer: {streamInfo.Peer}");
            }

            _checkTimer.Stop();
            _checkTimer.Dispose();

            _pingTimer.Stop();
            _pingTimer.Dispose();
        }

        public void Stop()
        {
            foreach (var streamInfo in _streamList)
            {
                streamInfo.CompletionTask.TrySetResult(1);
                Console.WriteLine($"Remove stream connect (peer: {streamInfo.Peer})");
            }

            _checkTimer.Stop();
            _pingTimer.Stop();
        }

        private void RemoveStream(StreamData<T> streamData)
        {
            streamData.CompletionTask.TrySetResult(1);
            _streamList.Remove(streamData);
            Console.WriteLine($"Remove stream connect (peer: {streamData.Peer})");
        }

        private Task CheckStreams(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            var streamsToRemove = _streamList
                .Where(x => x.CancelationToken.HasValue && x.CancelationToken.Value.IsCancellationRequested)
                .ToList();

            foreach (var streamData in streamsToRemove)
            {
                RemoveStream(streamData);
            }

            return Task.CompletedTask;
        }

        private Task Ping(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            foreach (var streamData in _streamList)
            {
                var instance = streamData.LastSentData;

                try
                {
                    streamData.Stream.WriteAsync(instance)
                        .ContinueWith(t => RemoveStream(streamData), TaskContinuationOptions.OnlyOnFaulted);
                }
                catch {}
            }

            return Task.CompletedTask;
        }
    }

    internal class StreamData<T> : StreamInfo<T> where T : class
    {
        public TaskCompletionSource<int> CompletionTask { get; set; }
        public T LastSentData { get; set; }
        public bool KeepLastData { get; set; }

        public static StreamData<T> Create(StreamInfo<T> streamInfo, List<T> initData = null)
        {
            return new StreamData<T>
            {
                CompletionTask = new TaskCompletionSource<int>(),
                CancelationToken = streamInfo.CancelationToken,
                Stream = streamInfo.Stream,
                Keys = streamInfo.Keys,
                Peer = streamInfo.Peer,
                LastSentData = initData?.Last(),
                KeepLastData = initData != null
            };
        }
    }
}
