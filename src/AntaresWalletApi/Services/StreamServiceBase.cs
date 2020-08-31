using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using Common;
using Common.Log;
using Lykke.Common.Log;

namespace AntaresWalletApi.Services
{
    public class StreamServiceBase<T> where T : class
    {
        private readonly List<StreamData<T>> _streamList = new List<StreamData<T>>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly TimerTrigger _checkTimer;
        private readonly TimerTrigger _pingTimer;
        private readonly TimerTrigger _jobTimer;
        private readonly ILog _log;

        public StreamServiceBase(ILogFactory logFactory, bool needPing = false, TimeSpan? jobPeriod = null)
        {
            _log = logFactory.CreateLog(this);
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

        internal virtual Task WriteStreamDataAsync(StreamData<T> streamData, T data)
        {
            return WriteStreamAsync(streamData, data);
        }

        internal virtual Task ProcesJobAsync(List<StreamData<T>> streamList)
        {
            return Task.CompletedTask;
        }

        protected Task WriteToStreamAsync(StreamData<T> streamData, T data)
        {
            return WriteStreamAsync(streamData, data);
        }

        public Task WriteToStreamAsync(T data, string key = null)
        {
            StreamData<T>[] items;
            _lock.EnterReadLock();
            try
            {
                items = string.IsNullOrEmpty(key)
                    ? _streamList.ToArray()
                    : _streamList.Where(x =>
                            x.Keys.Contains(key, StringComparer.InvariantCultureIgnoreCase) || x.Keys.Length == 0)
                        .ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            items = items.Where(x => !x.CancelationToken?.IsCancellationRequested ?? true).ToArray();

            var tasks = new List<Task>();

            foreach (var streamData in items)
            {
                var processedData = ProcessDataBeforeSend(data, streamData);
                streamData.LastSentData = streamData.KeepLastData ? data : null;
                tasks.Add(WriteStreamAsync(streamData, processedData));
            }

            return Task.WhenAll(tasks);
        }

        public async Task<Task> RegisterStreamAsync(StreamInfo<T> streamInfo, List<T> initData = null)
        {
            var data = StreamData<T>.Create(streamInfo, initData);

            _lock.EnterWriteLock();

            try
            {
                _streamList.Add(data);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (initData == null)
                return data.CompletionTask.Task;

            var tasks = initData.Select(value => WriteStreamAsync(data, value)).ToList();

            await Task.WhenAll(tasks);

            return data.CompletionTask.Task;
        }

        public void Dispose()
        {
            CloseAllStreams();

            _checkTimer.Stop();
            _checkTimer.Dispose();

            _pingTimer.Stop();
            _pingTimer.Dispose();

            _jobTimer.Stop();
            _jobTimer.Dispose();
        }

        public void Stop()
        {
            CloseAllStreams();

            _checkTimer.Stop();
            _pingTimer.Stop();
            _jobTimer.Stop();
        }

        private void CloseAllStreams()
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var streamInfo in _streamList)
                {
                    streamInfo.CompletionTask.TrySetResult(1);
                    Console.WriteLine($"Remove stream connect (peer: {streamInfo.Peer}");
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void RemoveStream(StreamData<T> streamData)
        {
            _lock.EnterWriteLock();
            try
            {
                streamData.CompletionTask.TrySetResult(1);
                _streamList.Remove(streamData);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            Console.WriteLine($"Remove stream connect (peer: {streamData.Peer})");
        }

        private Task CheckStreams(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            List<StreamData<T>> streamsToRemove;
            _lock.EnterReadLock();
            try
            {
                streamsToRemove = _streamList
                    .Where(x => x.CancelationToken.HasValue && x.CancelationToken.Value.IsCancellationRequested)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            foreach (var streamData in streamsToRemove)
            {
                RemoveStream(streamData);
            }

            return Task.CompletedTask;
        }

        private async Task Ping(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            var tasks = new List<Task>();

            if (_streamList.Count == 0)
                return;

            for (var i = _streamList.Count - 1; i >= 0; i--)
            {
                var streamData = _streamList[i];
                var instance = streamData.LastSentData ?? Activator.CreateInstance<T>();

                var data = ProcessPingDataBeforeSend(instance, streamData);
                tasks.Add(WriteStreamAsync(streamData, data));
            }

            if (tasks.Any())
                await Task.WhenAll(tasks);
        }

        private Task Job(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationtoken)
        {
            _lock.EnterReadLock();
            List<StreamData<T>> streams;
            try
            {
                streams = _streamList
                    .Where(x => !x.CancelationToken?.IsCancellationRequested ?? true)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return ProcesJobAsync(streams);
        }

        private async Task WriteStreamAsync(StreamData<T> streamData, T data)
        {
            try
            {
                await streamData.Stream.WriteAsync(data);
            }
            catch (InvalidOperationException)
            {
                RemoveStream(streamData);
            }
            catch (Exception e)
            {
                _log.Error(e, "Can't write to stream", context: streamData.Peer);
                RemoveStream(streamData);
            }
        }
    }
}
