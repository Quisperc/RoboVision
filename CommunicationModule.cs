// ======================== 通信模块 CommunicationModule.cs ========================
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RoboVision
{
    public class CommunicationModule : IDisposable
    {
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

                StatusChanged?.Invoke(this, $"服务器已启动 {ip}:{port}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"启动失败：{ex.Message}");
            }
        }

        private void ListenForClients()
        {
            while (_running)
            {
                try
                {
                    var client = _server.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    break;
                }
            }
        }

        private void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    DataReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"客户端错误：{ex.Message}");
            }
        }

        public void SendToClient(string ip, int port, string message)
        {
            try
            {
                var client = new TcpClient(ip, port);
                var stream = client.GetStream();
                var data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
                StatusChanged?.Invoke(this, $"数据已发送至 {ip}:{port}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"发送失败：{ex.Message}");
            }
        }

        public void Dispose()
        {
            _running = false;
            _server?.Stop();
            _listenerThread?.Join(1000);
        }
    }
}
