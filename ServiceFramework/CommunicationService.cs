﻿using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace ServiceFramework
{
    public class CommunicationService
    {
        public Boolean stop = false;

        public String pipeName { get; private set; }

        //信息收到时触发的事件，用于处理接收到的参数。
        public event Action<MessageEventArgs> ReceivedMessage;

        public event Action<SupervisorEventArgs> ReturnedMessage;

        /// <summary>
        /// 开始侦听。
        /// </summary>
        public void ListenAsHost()
        {
            //测试结束，如果存在一个实例，此处将会引发错误。
            Task.Run(() =>
            {
                while (!stop)
                {
                    using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte))
                    {
                        //等待客户端连接。
                        server.WaitForConnection();
                        using (StreamReader sr = new StreamReader(server))
                        {
                            //调用事件参数构造函数解析参数。
                            var EventArgs = new MessageEventArgs(JsonHelper<String[]>.DeSerialize(sr.ReadToEnd()));
                            if (EventArgs.Order == Order.terminate)
                            {
                                //如果收到停止命令则不再开启侦听。
                                stop = true;
                            }
                            ReceivedMessage(EventArgs);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 开始接收回传数据。
        /// </summary>
        public void ListenAsSupervisor()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using (var server = new NamedPipeServerStream(pipeName + "Supervisor", PipeDirection.InOut, 10, PipeTransmissionMode.Byte))
                    {
                        //等待客户端连接。
                        server.WaitForConnection();
                        using (StreamReader sr = new StreamReader(server))
                        {
                            var SupervisorMessage = JsonHelper<SupervisorEventArgs>.DeSerialize(sr.ReadToEnd());
                            ReturnedMessage(SupervisorMessage);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 发送消息到服务执行程序。
        /// </summary>
        /// <param name="msg"></param>
        public Boolean SendToHost(String[] msg)
        {
            using (var client = new NamedPipeClientStream(pipeName))
            {
                return client.SendMessage(msg);
            }
        }

        /// <summary>
        /// 发送消息到外部命令客户端。
        /// </summary>
        /// <param name="msg"></param>
        public Boolean SendToSupervisor(SupervisorEventArgs msg)
        {
            using (var client = new NamedPipeClientStream(pipeName + "Supervisor"))
            {
                return client.SendMessage(msg);
            }
        }

        /// <summary>
        /// 命名管道构建。
        /// </summary>
        /// <param name="pipeName">名称。</param>
        public CommunicationService(String pipeName)
        {
            this.pipeName = pipeName;
        }
    }

    public static class NamedPipeExtender
    {
        public static bool SendMessage<T>(this NamedPipeClientStream client, T msg, Int32 timeout = 1000) where T : class
        {
            if (Utils.InvokeSomethingWithoutWatching(() => { client.ConnectAsync(timeout).Wait(); }) == "")
            {
                using (StreamWriter sw = new StreamWriter(client, Encoding.Unicode))
                {
                    sw.AutoFlush = true;
                    sw.Write(JsonHelper<T>.Serialize(msg));
                }
                return true;
            }
            return false;
        }
    }
}
