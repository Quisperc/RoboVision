// ======================== CommunicationModule.cs ========================
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoboVision
{
    public class CommunicationModule : IDisposable
    {
        // 接收服务器配置
        private const int RECEIVE_PORT = 8001;
        private TcpListener _receiveServer;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // 接收客户端管理
        private ConcurrentDictionary<TcpClient, byte> _receiveClients = new ConcurrentDictionary<TcpClient, byte>();
        
        // 添加连接超时和重试机制
        private const int CONNECTION_TIMEOUT_MS = 3000;
        private const int MAX_SEND_RETRIES = 2;

        // 事件定义
        public event EventHandler<string> DataReceived;
        public event EventHandler<string> StatusChanged;
        
        // 释放标志
        private bool _isDisposed;

        /// <summary>
        /// 启动接收服务器
        /// </summary>
        public async Task StartReceiveServerAsync(string ip = "127.0.0.1")
        {
            try
            {
                // 确保任何之前的服务器实例已关闭
                StopServer();
                
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
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"服务器启动失败：{ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 停止服务器
        /// </summary>
        private void StopServer()
        {
            if (_receiveServer != null)
            {
                try
                {
                    _receiveServer.Stop();
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"停止服务器错误：{ex.Message}");
                }
                _receiveServer = null;
            }
        }

        /// <summary>
        /// 监听接收客户端连接
        /// </summary>
        private async Task ListenForReceiveClientsAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested && !_isDisposed)
                {
                    try
                    {
                        var client = await _receiveServer.AcceptTcpClientAsync();
                        
                        // 配置客户端连接
                        client.ReceiveTimeout = 10000; // 10秒接收超时
                        client.SendTimeout = 5000;     // 5秒发送超时
                        client.NoDelay = true;         // 禁用Nagle算法提高响应速度
                        
                        _receiveClients.TryAdd(client, 0);
                        _ = HandleReceiveClientAsync(client);
                    }
                    catch (ObjectDisposedException)
                    {
                        // 服务器被释放，正常退出
                        break;
                    }
                    catch (Exception ex)
                    {
                        StatusChanged?.Invoke(this, $"接受连接错误：{ex.Message}");
                        // 短暂延迟后继续监听
                        await Task.Delay(1000);
                    }
                }
            }
            catch (ObjectDisposedException) 
            { 
                // 正常停止 
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"监听循环错误：{ex.Message}");
            }
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
                    var buffer = new byte[8192]; // 增加缓冲区大小
                    var endpoint = client.Client.RemoteEndPoint.ToString();
                    StatusChanged?.Invoke(this, $"客户端已连接 {endpoint}");

                    while (!_cts.IsCancellationRequested && !_isDisposed)
                    {
                        try
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                            if (bytesRead == 0) break; // 连接关闭

                            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            DataReceived?.Invoke(this, message);
                        }
                        catch (OperationCanceledException)
                        {
                            // 取消操作，正常退出
                            break;
                        }
                        catch (IOException ex)
                        {
                            StatusChanged?.Invoke(this, $"读取数据错误：{ex.Message}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"接收处理错误：{ex.Message}");
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
            if (_isDisposed) throw new ObjectDisposedException(nameof(CommunicationModule));
            if (string.IsNullOrEmpty(message)) return;
            
            int retryCount = 0;
            
            while (retryCount <= MAX_SEND_RETRIES)
            {
                using (var client = new TcpClient())
                {
                    try
                    {
                        // 设置连接超时
                        var connectTask = client.ConnectAsync(targetIp, targetPort);
                        var timeoutTask = Task.Delay(CONNECTION_TIMEOUT_MS);
                        
                        // 等待连接或超时
                        await Task.WhenAny(connectTask, timeoutTask);
                        
                        if (!client.Connected)
                        {
                            throw new TimeoutException($"连接到 {targetIp}:{targetPort} 超时");
                        }
                        
                        // 配置客户端连接
                        client.SendTimeout = 5000;     // 5秒发送超时
                        client.NoDelay = true;         // 提高响应速度
                        
                        // 发送数据
                        var data = Encoding.UTF8.GetBytes(message);
                        await client.GetStream().WriteAsync(data, 0, data.Length);
                        
                        StatusChanged?.Invoke(this, $"成功发送到 {targetIp}:{targetPort}");
                        return; // 成功发送，退出
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        
                        if (retryCount > MAX_SEND_RETRIES)
                        {
                            StatusChanged?.Invoke(this, $"发送到 {targetIp}:{targetPort} 失败：{ex.Message}");
                            throw;
                        }
                        else
                        {
                            StatusChanged?.Invoke(this, $"发送失败，第 {retryCount} 次重试中...");
                            await Task.Delay(500 * retryCount); // 逐步增加重试间隔
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 取消所有异步操作
                        _cts.Cancel();
                        
                        // 停止服务器
                        StopServer();
                        
                        // 关闭所有接收客户端
                        foreach (var client in _receiveClients.Keys)
                        {
                            try
                            {
                                client.Close();
                                client.Dispose();
                            }
                            catch (Exception ex)
                            {
                                StatusChanged?.Invoke(this, $"释放客户端错误：{ex.Message}");
                            }
                        }
                        _receiveClients.Clear();
                        
                        // 释放CTS
                        _cts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        StatusChanged?.Invoke(this, $"释放资源错误：{ex.Message}");
                    }
                }
                
                _isDisposed = true;
            }
        }
        
        ~CommunicationModule()
        {
            Dispose(false);
        }
    }
}
