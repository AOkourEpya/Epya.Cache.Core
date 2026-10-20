using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Epya.Cache.Common.Tcp
{
    public class TcpManager
    {
        public string ServerIp { get; set; }
        public int? Port { get; set; }
        public Func<string, string> OnMessageReceived;
        public Func<string, Task<string>> OnMessageReceivedAsync;
        public Func<string,string> OnMessageSent;
        public Func<string,Task<string>> OnMessageSentAsync;


        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create(65536, 300);// 64 KB buffers, 100 max
        private TcpListener tcpListener;
        private volatile bool isRunning = false;

        public TcpManager(string serverIp, int port)
        {
            ServerIp = serverIp;
            Port = port;
        }
        public TcpManager(int port)
        {
            Port = port;
        }


        public async Task<string> SendAsync(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(ServerIp) || !Port.HasValue)
                    throw new Exception("Server IP or Port is NULL.");

                string responseMessage = "";
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;  // Prevent TCP delays
                    client.SendBufferSize = 256 * 1024;
                    client.ReceiveBufferSize = 256 * 1024;
                    //client.LingerState = new LingerOption(false, 0);


                    await client.ConnectAsync(ServerIp, Port.Value);

                    using (NetworkStream stream = client.GetStream())
                    {
                        // Send message
                        await _WriteTcpMessageAsync(stream, message);
                        // Read response with pooled buffer
                        responseMessage = await _ReadTcpMessageAsync(stream);
                    }
                }

                return responseMessage;
            }
            catch
            {
                throw;
            }
        }
        public string Send(string message)
        {
            if (string.IsNullOrEmpty(ServerIp) || !Port.HasValue)
                throw new Exception("Server IP or Port is NULL.");

            try
            {
                string responseMessage = "";
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;  // Prevent TCP delays
                    client.SendBufferSize = 256 * 1024;
                    client.ReceiveBufferSize = 256 * 1024;
                    //client.LingerState = new LingerOption(false, 0);


                    client.Connect(ServerIp, Port.Value);

                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.ReadTimeout = 10000; // Set read timeout to 10 seconds

                        // Send message
                        _WriteTcpMessage(stream, message);
                        // Read response
                        responseMessage = _ReadTcpMessage(stream);
                    }
                }

                return responseMessage;
            }
            catch
            {
                throw;
            }
        }
        public void StartServer()
        {
            if (!Port.HasValue)
                throw new Exception("Port is NULL.");

            StopServer();

            isRunning = true;
            tcpListener = new TcpListener(IPAddress.Any, Port.Value);
            tcpListener.Server.NoDelay = true;
            tcpListener.Server.SendBufferSize = 256 * 1024;
            tcpListener.Server.ReceiveBufferSize = 256 * 1024;
            ThreadPool.SetMinThreads(200, 400);
            tcpListener.Start();

            _ = Task.Run(() => _AcceptClients());

        }
        public void StartServerAsync()
        {
            if (!Port.HasValue)
                throw new Exception("Port is NULL.");

            StopServer();

            isRunning = true;
            tcpListener = new TcpListener(IPAddress.Any, Port.Value);
            tcpListener.Server.NoDelay = true;
            tcpListener.Server.SendBufferSize = 256 * 1024;
            tcpListener.Server.ReceiveBufferSize = 256 * 1024;
            ThreadPool.SetMinThreads(200, 400);
            tcpListener.Start();

            _ = Task.Run(() => _AcceptClientsAsync());

        }
        public void StopServer()
        {
            isRunning = false;
            if (tcpListener != null)
                tcpListener.Stop();

        }


        private void _AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    client.NoDelay = true;  // Prevent TCP delays
                    client.SendBufferSize = 256 * 1024;
                    client.ReceiveBufferSize = 256 * 1024;
                    //client.LingerState = new LingerOption(false, 0);

                    _ = Task.Run(() => _HandleClient(client));
                }
                catch (SocketException)
                {
                    if (!isRunning) break; // Exit if server is stopping
                }
            }
        }
        private async Task _AcceptClientsAsync()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    client.NoDelay = true;  // Prevent TCP delays
                    client.SendBufferSize = 256 * 1024;
                    client.ReceiveBufferSize = 256 * 1024;
                    //client.LingerState = new LingerOption(false, 0);

                    _ = Task.Run(() => _HandleClientAsync(client));
                }
                catch (SocketException)
                {
                    if (!isRunning) break; // Exit if server is stopping
                }
            }
        }
        private async Task _HandleClientAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000; // Set read timeout to 10 seconds

                    string message = await _ReadTcpMessageAsync(stream);
                    string res = "";
                    if (OnMessageReceivedAsync != null)                    
                        res = await OnMessageReceivedAsync.Invoke(message);

                    await _WriteTcpMessageAsync(stream, res);

                }
            }
            catch
            {
                throw;
            }
            finally
            {
                client.Close(); // Ensure the client is closed after handling
            }

        }
        private void _HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000; // Set read timeout to 10 seconds

                    string message = _ReadTcpMessage(stream);
                    string res = "";
                    if (OnMessageReceived != null)
                        res = OnMessageReceived.Invoke(message);

                    _WriteTcpMessage(stream, "");
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                client.Close(); // Ensure the client is closed after handling
            }

        }
        private async Task<string> _ReadTcpMessageAsync(NetworkStream stream)
        {
            stream.ReadTimeout = 10000; // Set read timeout to 10 seconds

            string responseMessage = "";
            byte[] buffer = BufferPool.Rent(65536); // 64 KB buffer
            try
            {
                StringBuilder chunks = new StringBuilder();

                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    chunks.Append(chunk);
                    if (chunks.ToString().Contains("<END>"))
                        break;
                }

                responseMessage = chunks.ToString();
                if (responseMessage.EndsWith("<END>"))
                    responseMessage = responseMessage.Substring(0, responseMessage.Length - 5);

                return responseMessage;

            }
            catch
            {
                throw;
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }
        private string _ReadTcpMessage(NetworkStream stream)
        {
            stream.ReadTimeout = 10000; // Set read timeout to 10 seconds

            string responseMessage = "";
            byte[] buffer = BufferPool.Rent(65536); // 64 KB buffer
            try
            {
                StringBuilder chunks = new StringBuilder();

                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    chunks.Append(chunk);
                    if (chunks.ToString().Contains("<END>"))
                        break;
                }

                responseMessage = chunks.ToString();
                if (responseMessage.EndsWith("<END>"))
                    responseMessage = responseMessage.Substring(0, responseMessage.Length - 5);

                return responseMessage;

            }
            catch
            {
                throw;
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }
        private async Task _WriteTcpMessageAsync(NetworkStream stream, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "<END>");
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();  // Ensure immediate transmission
                if (OnMessageSent != null)
                    await OnMessageSentAsync.Invoke(message);
            }
            catch {
                throw;
            }
        }
        private void _WriteTcpMessage(NetworkStream stream, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "<END>");
                stream.Write(data, 0, data.Length);
                stream.Flush();  // Ensure immediate transmission
                if (OnMessageSent != null)
                    OnMessageSent.Invoke(message);
            }
            catch {
                throw;
            }
        }

    }
}
