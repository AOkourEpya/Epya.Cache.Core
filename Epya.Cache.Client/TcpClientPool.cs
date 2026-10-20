using Epya.Cache.Common;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace Epya.Cache.Client
{
    public class TcpClientPool
    {
        private readonly string _serverAddress;
        private readonly string _logFilePath;
        private readonly int _port;
        private readonly int _maxPoolSize;
        private readonly ConcurrentBag<TcpClient> _pool;
        private readonly SemaphoreSlim _semaphore;

        public TcpClientPool(CacheConfigDto configs, int port, int maxPoolSize = 10)
        {
            ArgumentNullException.ThrowIfNull(configs);

            _serverAddress = configs.ServerAddress;
            _logFilePath = configs.LogFilePath;
            _port = port;
            _maxPoolSize = maxPoolSize;
            _pool = new ConcurrentBag<TcpClient>();
            _semaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);
        }

        public async Task<TcpClient> GetClientAsync()
        {

            Stopwatch stopwatch = Stopwatch.StartNew();           

            await _semaphore.WaitAsync(); // Wait for available slot

            long _semaphoretime = stopwatch.ElapsedMilliseconds;
            long getPoolClientTime=0;
            long getNewClientTime=0;

            if (_pool.TryTake(out TcpClient client) && IsClientConnected(client))
            {
                getPoolClientTime = stopwatch.ElapsedMilliseconds - _semaphoretime;
                _ = Task.Run(() =>
                      LogToFile($"[Cache] Get Client Total time : {stopwatch.Elapsed.Milliseconds / 1000} seconds, " +
                                $"_semaphoretime: {_semaphoretime / 1000.0} seconds & getPoolClientTime : {getPoolClientTime / 1000.0} seconds"))
                      .ConfigureAwait(false);
            }
            else
            {
                client = await CreateClientAsync();
                getNewClientTime = stopwatch.ElapsedMilliseconds - _semaphoretime;
                _ = Task.Run(() =>
                      LogToFile($"[Cache] Get Client Total time : {stopwatch.Elapsed.Milliseconds / 1000} seconds, " +
                                $"_semaphoretime: {_semaphoretime / 1000.0} seconds & getNewClientTime : {getNewClientTime / 1000.0} seconds" + $" pool size : {_pool.Count}"))
                      .ConfigureAwait(false);
            }

            stopwatch.Stop();

          

            return client;
        }

        public void ReturnClient(TcpClient client)
        {
            _ = Task.Run(() =>
                      LogToFile($"[Cache] ReturnClient is Client null: "+((client==null)?"yes":"no")+$", is Client Connected : {IsClientConnected(client)}"+ $", client.Connected : {client.Connected}")).ConfigureAwait(false);

            if (client == null || !IsClientConnected(client))
            {
                client?.Dispose(); // Dispose null or broken client
            }
            else
            {
                _pool.Add(client); // Return valid client to pool
            }

            _semaphore.Release(); // Always release the slot
        }

        private async Task<TcpClient> CreateClientAsync()
        {
            var client = new TcpClient
            {
                NoDelay = true,
                SendBufferSize = 256 * 1024,
                ReceiveBufferSize = 256 * 1024,
                LingerState = new LingerOption(false, 0)
            };

            await client.ConnectAsync(_serverAddress, _port);
            return client;
        }

        private bool IsClientConnected(TcpClient client)
        {
            try
            {
                return client.Connected && client.Client.Poll(0, SelectMode.SelectWrite);
            }
            catch
            {
                return false;
            }
        }


        private static readonly object fileLock = new object();
        private void LogToFile(string message)
        {
            if (string.IsNullOrWhiteSpace(_logFilePath))
                return;

            string logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}";

            try
            {
                lock (fileLock)
                {
                    using (StreamWriter writer = File.AppendText(_logFilePath))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle logging error (optional)
                Trace.WriteLine($"[Log Error] Failed to write to log file: {ex.Message}");
            }
        }
    }
}