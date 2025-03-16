// ======================== CommunicationModule.cs ========================
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RailwayRoadSectionDetection
{
    public class CommunicationModule : IDisposable
    {
        // 接收服务器配置
        private const int RECEIVE_PORT = 8001;
        private TcpListener _receiveServer;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // 接收客户端管理
        private ConcurrentDictionary<TcpClient, byte> _receiveClients = new ConcurrentDictionary<TcpClient, byte>();

        // 事件定义
        public event EventHandler<string> DataReceived;
        public event EventHandler<string> StatusChanged;

        /// <summary>
        /// 启动接收服务器
        /// </summary>
        //public async Task StartReceiveServerAsync(string ip = "127.0.0.1")
        //{
        //    try
        //    {
        //        _receiveServer = new TcpListener(IPAddress.Parse(ip), RECEIVE_PORT);
        //        _receiveServer.Start();
        //        _ = ListenForReceiveClientsAsync();
        //        StatusChanged?.Invoke(this, $"接收服务器已启动 {ip}:{RECEIVE_PORT}");
        //    }
        //    catch (Exception ex)
        //    {
        //        StatusChanged?.Invoke(this, $"服务器启动失败：{ex.Message}");
        //        throw;
        //    }
        //}
        public async Task StartReceiveServerAsync(string ip = "127.0.0.1")
        {
            try
            {
                _receiveServer = new TcpListener(IPAddress.Parse(ip), RECEIVE_PORT);
                _receiveServer.Start();

                // 创建独立监控任务
                var listenTask = ListenForReceiveClientsAsync();

                // 注册异常监控
                _ = listenTask.ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        StatusChanged?.Invoke(this, $"监听异常：{t.Exception.Flatten().InnerException.Message}");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

                StatusChanged?.Invoke(this, $"接收服务器已启动 {ip}:{RECEIVE_PORT}");

                // 保持异步上下文（根据需求二选一）
                await listenTask; // 方案1：等待任务完成（适合需要同步启动的场景）
                // return listenTask; // 方案2：返回任务本身（保持完全异步） 
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"服务器启动失败：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 监听接收客户端连接
        /// </summary>
        private async Task ListenForReceiveClientsAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _receiveServer.AcceptTcpClientAsync();
                    _receiveClients.TryAdd(client, 0);
                    _ = HandleReceiveClientAsync(client);
                }
            }
            catch (ObjectDisposedException) { /* 正常停止 */ }
        }

        /// <summary>
        /// 处理接收客户端数据
        /// </summary>
        private async Task HandleReceiveClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    StatusChanged?.Invoke(this, $"客户端已连接 {client.Client.RemoteEndPoint}");

                    while (!_cts.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                        if (bytesRead == 0) break;

                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        DataReceived?.Invoke(this, message);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"接收错误：{ex.Message}");
            }
            finally
            {
                _receiveClients.TryRemove(client, out _);
                StatusChanged?.Invoke(this, $"客户端断开");
            }
        }

        /// <summary>
        /// 发送数据到指定端点（客户端模式）
        /// </summary>
        public async Task SendDataAsync(string targetIp, int targetPort, string message)
        {
            using (var client = new TcpClient()) // 修改的关键点
            {
                try
                {
                    await client.ConnectAsync(targetIp, targetPort);
                    var data = Encoding.UTF8.GetBytes(message);
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                    StatusChanged?.Invoke(this, $"成功发送到 {targetIp}:{targetPort}");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"发送到 {targetIp}:{targetPort} 失败：{ex.Message}");
                    throw;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _receiveServer?.Stop();
                // 关闭所有接收客户端
                foreach (var client in _receiveClients.Keys)
                {
                    client.Dispose();
                }
                _receiveClients.Clear();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"释放资源错误：{ex.Message}");
            }
        }
    }
}
