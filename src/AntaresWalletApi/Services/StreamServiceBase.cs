using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using Common;
using Lykke.Common.Log;

namespace AntaresWalletApi.Services
{
    public class StreamServiceBase<T> where T : class
    {
        private readonly List<StreamData<T>> _streamList = new List<StreamData<T>>();
        private readonly TimerTrigger _checkTimer;
        private readonly TimerTrigger _pingTimer;
        private readonly TimerTrigger _jobTimer;

        public StreamServiceBase(ILogFactory logFactory, bool needPing = false, TimeSpan? jobPeriod = null)
        {
            _checkTimer = new TimerTrigger($"StreamService<{nameof(T)}>", TimeSpan.FromSeconds(10), logFactory);
            _checkTimer.Triggered += CheckStreams;
            _checkTimer.Start();

            if (jobPeriod.HasValue)
            {
                _jobTimer = new TimerTrigger($"StreamService<{nameof(T)}>", jobPeriod.Value, logFactory);
                _jobTimer.Triggered += Job;
                _jobTimer.Start();
            }

            if (needPing)
            {
                _pingTimer = new TimerTrigger($"StreamService<{nameof(T)}>", TimeSpan.FromSeconds(30), logFactory);
                _pingTimer.Triggered += Ping;
                _pingTimer.Start();
            }
        }

        internal virtual T ProcessDataBeforeSend(T data, StreamData<T> streamData)
        {
            return data;
        }

        internal virtual T ProcessPingDataBeforeSend(T data, StreamData<T> streamData)
        {
            return data;
        }

        internal virtual void WriteStreamData(StreamData<T> streamData, T data)
        {
            WriteToStream(streamData, data);
        }

        protected void WriteToStream(StreamData<T> streamData, T data)
        {
            streamData.Stream.WriteAsync(data)
                .ContinueWith(t => RemoveStream(streamData), TaskContinuationOptions.OnlyOnFaulted);
        }

        internal virtual Task ProcesJobAsync(List<StreamData<T>> streamList)
        {
            return Task.CompletedTask;
        }

        public void WriteToStream(T data, string key = null)
        {
            var items = string.IsNullOrEmpty(key)
                ? _streamList.ToArray()
                : _streamList.Where(x => x.Keys.Contains(key, StringComparer.InvariantCultureIgnoreCase) || x.Keys.Length == 0).ToArray();

            items = items.Where(x => !x.CancelationToken?.IsCancellationRequested ?? true).ToArray();

            foreach (var streamData in items)
            {
                var processedData = ProcessDataBeforeSend(data, streamData);
                streamData.LastSentData = streamData.KeepLastData ? data : null;
                WriteStreamData(streamData, processedData);
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

            _jobTimer.Stop();
            _jobTimer.Dispose();
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
            _jobTimer.Stop();
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
                var instance = streamData.LastSentData ?? Activator.CreateInstance<T>();

                try
                {
                    var data = ProcessPingDataBeforeSend(instance, streamData);
                    WriteStreamData(streamData, data);
                }
                catch {}
            }

            return Task.CompletedTask;
        }

        private Task Job(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            var streams = _streamList
                .Where(x => !x.CancelationToken?.IsCancellationRequested ?? true)
                .ToList();

            return ProcesJobAsync(streams);
        }
    }
}
