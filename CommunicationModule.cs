// ======================== CommunicationModule.cs ========================
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RoboVision
{
    public class CommunicationModule : IDisposable
    {
        // 新增取消令牌源
        private CancellationTokenSource _cts = new CancellationTokenSource();
        public event EventHandler<string> DataReceived;
        public event EventHandler<string> StatusChanged;

        private TcpListener _server;
        private bool _running;
        private Thread _listenerThread;

        public void StartServer(string ip, int port)
        {
            try
            {
                _server = new TcpListener(IPAddress.Parse(ip), port);
                _server.Start();
                _running = true;

                _listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true
                };
                _listenerThread.Start();

                StatusChanged?.Invoke(this, $"通信服务器连接成功，地址 {ip}:{port}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"通信服务器连接失败：{ex.Message}");
            }
        }

        private void ListenForClients()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_server.Pending()) // 检查待处理连接
                    {
                        var client = _server.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(HandleClient, client);
                    }
                    else
                    {
                        Thread.Sleep(100); // 减少CPU占用
                    }
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                // 正常关闭时的预期异常
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"监听线程异常：{ex.Message}");
            }
        }

        private void HandleClient(object state)
        {
            using (var client = (TcpClient)state)
            using (var stream = client.GetStream())
            {
                try
                {
                    var buffer = new byte[4096];
                    while (!_cts.IsCancellationRequested && client.Connected)
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            DataReceived?.Invoke(this, message);
                        }
                        else
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
                catch (IOException ex) when ((ex.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionReset)
                {
                    StatusChanged?.Invoke(this, "客户端强制断开连接");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"客户端处理异常：{ex.Message}");
                }
            }
        }

        public void SendToClient(string ip, int port, string message)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(ip, port);
                    using (var stream = client.GetStream())
                    {
                        var data = Encoding.UTF8.GetBytes(message);
                        stream.Write(data, 0, data.Length);
                        StatusChanged?.Invoke(this, $"数据已发送至 {ip}:{port}");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"发送失败：{ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                // 有序关闭流程
                _cts.Cancel();
                _server?.Stop();
                _listenerThread?.Join(1000);

                // 确保资源释放
                _server?.Server?.Close();
                _server?.Server?.Dispose();
            }
            catch (ObjectDisposedException) { /* 已释放对象无需处理 */ }
        }
    }
}
