using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

//namespace RoboVision
//{
//    public partial class CommunicationModule : Form
//    {
//        private string ipAddress;
//        private int port;

//        public CommunicationModule(string ip, int port)
//        {
//            this.ipAddress = ip;
//            this.port = port;
//        }

//        // 将坐标数据以字符串格式发送（例如通过TCP/IP）
//        public void SendData(int x, int y)
//        {
//            string message = $"X:{x},Y:{y}";
//            try
//            {
//                TcpClient client = new TcpClient(ipAddress, port);
//                NetworkStream stream = client.GetStream();
//                byte[] data = Encoding.ASCII.GetBytes(message);
//                stream.Write(data, 0, data.Length);
//                stream.Close();
//                client.Close();
//                MessageBox.Show("坐标数据已发送。");
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show("发送数据失败：" + ex.Message);
//            }
//        }
//    }
//}
namespace RoboVision
{
    public class CommunicationModule
    {
        private string ipAddress;
        private int port;

        // 用于服务器模式：监听客户端连接
        private TcpListener server;
        private bool serverRunning = false;

        public CommunicationModule(string ip, int port)
        {
            this.ipAddress = ip;
            this.port = port;

            // 启动服务器监听
            StartServer();
        }

        #region 客户端功能

        /// <summary>
        /// 客户端发送数据：将坐标数据以字符串格式发送到指定 IP 和端口。
        /// </summary>
        public void SendData(int x, int y)
        {
            string message = $"X:{x},Y:{y}";
            try
            {
                // 建立 TcpClient 连接并发送数据
                using (TcpClient client = new TcpClient(ipAddress, port))
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] data = Encoding.ASCII.GetBytes(message);
                    stream.Write(data, 0, data.Length);
                    //// 发送的数据接收方可以通过读取 NetworkStream 获取数据
                    //byte[] buffer = new byte[1024];
                    //int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    //string received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                }
                MessageBox.Show("坐标数据：(" + message + ")已发送。", "发送成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送数据失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 服务器功能

        /// <summary>
        /// 启动服务器，监听指定 IP 和端口
        /// </summary>
        private void StartServer()
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse(ipAddress);
                server = new TcpListener(localAddr, port);
                server.Start();
                serverRunning = true;

                // 启动后台线程监听客户端连接
                Thread serverThread = new Thread(ListenForClients)
                {
                    IsBackground = true
                };
                serverThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动服务器失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 循环监听客户端连接
        /// </summary>
        private void ListenForClients()
        {
            while (serverRunning)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    // 为每个客户端启动一个新线程处理数据接收
                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true
                    };
                    clientThread.Start();
                }
                catch (SocketException)
                {
                    // 监听被关闭时，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("监听异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 处理接入的客户端连接并接收数据
        /// </summary>
        /// <param name="client">连接的客户端</param>
        private void HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    // 直接调用 MessageBox.Show
                    MessageBox.Show("坐标数据：(" + received + ")已接收。", "数据接收", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // 直接调用 MessageBox.Show
                MessageBox.Show("处理客户端数据时出错：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                client.Close();
            }
        }

        /// <summary>
        /// 停止服务器，释放资源
        /// </summary>
        public void StopServer()
        {
            try
            {
                serverRunning = false; // 标志位确保线程退出循环
                if (server != null)
                {
                    server.Stop();      // 触发 SocketException 使监听线程退出
                    server = null;      // 避免重复调用 Stop()
                }
            }
            catch (Exception ex)
            {
                // 使用日志记录替代弹窗
                Debug.WriteLine("停止服务器错误: " + ex.Message);
            }

            #endregion
        }
    }
}