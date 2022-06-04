using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BitConverter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BiliDMLib
{
    public class DanmakuLoader
    {
        private string[] defaulthosts = new string[] { "tx-gz-live-comet-02.chat.bilibili.com", "tx-bj-live-comet-02.chat.bilibili.com", "broadcastlv.chat.bilibili.com" };
        private int defaultport = 2243;
        private string ChatHost = "broadcastlv.chat.bilibili.com";
        private int ChatPort = 2243; // TCPЭ��Ĭ�϶˿������޸ĵ� 2243
        private TcpClient Client;
        private Stream NetStream;
        private string CIDInfoUrl = "https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id=";
        private bool Connected = false;
        public Exception Error;
        public event ReceivedDanmakuEvt ReceivedDanmaku;
        public event DisconnectEvt Disconnected;
        public event ReceivedRoomCountEvt ReceivedRoomCount;
        public event LogMessageEvt LogMessage;
        private bool debuglog = false;
        private short protocolversion = 1;
        private static int lastroomid ;
        private static string token = "";
        private static string lastserver;
        private static HttpClient httpClient=new HttpClient(){Timeout = TimeSpan.FromSeconds(5)};
        private static List<Tuple<string, int>> ChatHostList = new List<Tuple<string, int>>();
        private CancellationTokenSource cancellationTokenSource;

        public async Task<bool> ConnectAsync(int roomId)
        {
            try
            {
                if (this.Connected) throw new InvalidOperationException();
                var channelId = roomId;
//
//                var request = WebRequest.Create(RoomInfoUrl + roomId + ".json");
//                var response = request.GetResponse();
//
//                int channelId;
//                using (var stream = response.GetResponseStream())
//                using (var sr = new StreamReader(stream))
//                {
//                    var json = await sr.ReadToEndAsync();
//                    Debug.WriteLine(json);
//                    dynamic jo = JObject.Parse(json);
//                    channelId = (int) jo.list[0].cid;
//                }
               
                if (channelId != lastroomid || ChatHostList.Count==0)
                {
                    try
                    {
                        var req = await httpClient.GetStringAsync(CIDInfoUrl + channelId);
                        var roomobj = JObject.Parse(req);
                        token = roomobj["data"]["token"]+"";

                        var serverlist = roomobj["data"]["host_list"].Value<JArray>();
                        ChatHostList = new List<Tuple<string, int>>();
                        foreach (var serverinfo in serverlist)
                        {
                            ChatHostList.Add(new Tuple<string, int>(serverinfo["host"] + "", serverinfo["port"].Value<int>()));
                        }

                        var server = ChatHostList[new Random().Next(ChatHostList.Count)];
                        ChatHost = server.Item1;

                        ChatPort = server.Item2;
                        if (string.IsNullOrEmpty(ChatHost))
                        {
                            throw new Exception();
                        }
                  
                    }
                    catch (WebException ex)
                    {
                        ChatHost = defaulthosts[new Random().Next(defaulthosts.Length)];
                        ChatPort = defaultport;
                        var errorResponse = ex.Response as HttpWebResponse;
                        if (errorResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                            // ֱ���䲻���ڣ�HTTP 404��
                            var msg = "��ֱ�������Ʋ����ڣ���Ļ��ֻ֧��ʹ��ԭ���������";
                            LogMessage?.Invoke(this, new LogMessageArgs() {message = msg});
                        }
                        else
                        {
                            // Bվ��������Ӧ����
                            var msg = "Bվ��������Ӧ��Ļ��������ַ����������ʹ�ó�����ַ����";
                            LogMessage?.Invoke(this, new LogMessageArgs() {message = msg});
                        }
                    }
                    catch (Exception)
                    {
                        // ��������XML�������󣿣�
                        ChatHost = defaulthosts[new Random().Next(defaulthosts.Length)];
                        ChatPort = defaultport;
                        var msg = "��ȡ��Ļ��������ַʱ����δ֪���󣬳���ʹ�ó�����ַ����";
                        LogMessage?.Invoke(this, new LogMessageArgs() {message = msg});
                    }


                }
                else
                {
                    var server = ChatHostList[new Random().Next(ChatHostList.Count)];
                    ChatHost = server.Item1;

                    ChatPort = server.Item2;
                }
                Client = new TcpClient();

                var ipAddress = await System.Net.Dns.GetHostAddressesAsync(ChatHost);
                var random = new Random();
                var idx=random.Next(ipAddress.Length);
                await  Client.ConnectAsync(ipAddress[idx], ChatPort);

                NetStream = Stream.Synchronized(Client.GetStream());
                cancellationTokenSource = new CancellationTokenSource();

                if (await SendJoinChannel(channelId,token, cancellationTokenSource.Token))
                {
                    Connected = true;
                    _=this.ReceiveMessageLoop(cancellationTokenSource.Token);
                    lastserver = ChatHost;
                    lastroomid = roomId;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                this.Error = ex;
                return false;
            }
        }

        private async Task ReceiveMessageLoop(CancellationToken ct)
        {
            Task heartbeatLoop = null;
            try
            {
                var stableBuffer = new byte[16];
                var buffer = new byte[4096];
                while (this.Connected)
                {
                    await NetStream.ReadBAsync(stableBuffer, 0, 16,ct);
                    var protocol=DanmakuProtocol.FromBuffer(stableBuffer);
                    if (protocol.PacketLength < 16)
                    {
                        throw new NotSupportedException("Э��ʧ��: (L:" + protocol.PacketLength + ")");
                    }
                    var payloadlength = protocol.PacketLength - 16;
                    if (payloadlength == 0)
                    {
                        continue; // û��������
                    }
                    
                    buffer = new byte[payloadlength];
                    
                    await NetStream.ReadBAsync(buffer, 0, payloadlength,ct);
                    if (heartbeatLoop == null)
                    {
                        heartbeatLoop = this.HeartbeatLoop(cancellationTokenSource.Token);
                    }
                    if (protocol.Version == 2 && protocol.Action == 5) // ����deflate��Ϣ
                    {
                        using (var ms = new MemoryStream(buffer, 2, payloadlength - 2)) // Skip 0x78 0xDA
                        using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                        {
                            var headerbuffer = new byte[16];
                            try
                            {
                                while (true)
                                {
                                    await deflate.ReadBAsync(headerbuffer, 0, 16, ct);
                                    var protocol_in = DanmakuProtocol.FromBuffer(headerbuffer);
                                    payloadlength = protocol_in.PacketLength - 16;
                                    var danmakubuffer = new byte[payloadlength];
                                    await deflate.ReadBAsync(danmakubuffer, 0, payloadlength, ct);
                                    ProcessDanmaku(protocol.Action, danmakubuffer);
                                }
                              
                            }
                            catch (Exception e)
                            {
                                
                            }

                         
                        }
                    }
                    else if (protocol.Version == 3 && protocol.Action == 5) // brotli?
                    {
                        using (var ms = new MemoryStream(buffer)) // Skip 0x78 0xDA
                        
                        using (var deflate = new BrotliSharpLib.BrotliStream(ms, CompressionMode.Decompress))
                        {
                            var headerbuffer = new byte[16];
                            try
                            {
                                while (true)
                                {
                                    await deflate.ReadBAsync(headerbuffer, 0, 16, ct);
                                    var protocol_in = DanmakuProtocol.FromBuffer(headerbuffer);
                                    payloadlength = protocol_in.PacketLength - 16;
                                    var danmakubuffer = new byte[payloadlength];
                                    await deflate.ReadBAsync(danmakubuffer, 0, payloadlength, ct);
                                    ProcessDanmaku(protocol.Action, danmakubuffer);
                                }

                            }
                            catch (Exception e)
                            {

                            }


                        }
                    }
                    else
                    {
                        ProcessDanmaku(protocol.Action, buffer);
                    }
                }
            }
            //catch (NotSupportedException ex)
            //{
            //    this.Error = ex;
            //    _disconnect();
            //}
            catch (Exception ex)
            {
                this.Error = ex;
                _disconnect();
                if (debuglog)
                {
                    Console.WriteLine(ex);
                }
            }

            
        }

        private  void ProcessDanmaku(int action, byte[] buffer)
        {
            switch (action)
            {
                case 3: // (OpHeartbeatReply)
                    {
                        var viewer = EndianBitConverter.BigEndian.ToUInt32(buffer, 0); //��������
                        // Console.WriteLine(viewer);
                        ReceivedRoomCount?.Invoke(this, new ReceivedRoomCountArgs() { UserCount = viewer });
                        break;
                    }
                case 5://playerCommand (OpSendMsgReply)
                    {

                        var json = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                        if (debuglog)
                        {
                            Console.WriteLine(json);
                        }
                        try
                        {
                            var dama = new DanmakuModel(json, 2);
                            ReceivedDanmaku?.Invoke(this, new ReceivedDanmakuArgs() { Danmaku = dama });
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                        break;
                    }
                case 8: // (OpAuthReply)
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        private async Task HeartbeatLoop(CancellationToken cancellationToken)
        {

            try
            {
                while (this.Connected)
                {
                    await this.SendHeartbeatAsync(cancellationToken);
                    await Task.Delay(30000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex;
                _disconnect();
                if (debuglog)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public void Disconnect()
        {

            Connected = false;
            try
            {
                Client.Close();
            }
            catch (Exception)
            {

            }


            NetStream = null;
        }

        private void _disconnect()
        {
            if (Connected)
            {
                Debug.WriteLine("Disconnected");
                cancellationTokenSource.Cancel();
              
                Connected = false;

                Client.Close();
                
                NetStream = null;
                if (Disconnected != null)
                {
                    Disconnected(this, new DisconnectEvtArgs() {Error = Error});
                }
            }

        }

        private async Task SendHeartbeatAsync(CancellationToken ct)
        {
            await SendSocketDataAsync(2, "[object Object]", ct);
            Debug.WriteLine("Message Sent: Heartbeat");
        }

        Task SendSocketDataAsync(int action, string body , CancellationToken ct)
        {
            return SendSocketDataAsync(0, 16, protocolversion, action, 1, body,ct);
        }
        async Task SendSocketDataAsync(int packetlength, short magic, short ver, int action, int param, string body,CancellationToken ct)
        {
            var playload = Encoding.UTF8.GetBytes(body);
            if (packetlength == 0)
            {
                packetlength = playload.Length + 16;
            }
            var buffer = new byte[packetlength];
            using (var ms = new MemoryStream(buffer))
            {


                var b = EndianBitConverter.BigEndian.GetBytes(buffer.Length);

                await ms.WriteAsync(b, 0, 4);
                b = EndianBitConverter.BigEndian.GetBytes(magic);
                await ms.WriteAsync(b, 0, 2);
                b = EndianBitConverter.BigEndian.GetBytes(ver);
                await ms.WriteAsync(b, 0, 2);
                b = EndianBitConverter.BigEndian.GetBytes(action);
                await ms.WriteAsync(b, 0, 4);
                b = EndianBitConverter.BigEndian.GetBytes(param);
                await ms.WriteAsync(b, 0, 4);
                if (playload.Length > 0)
                {
                    await ms.WriteAsync(playload, 0, playload.Length);
                }
                await NetStream.WriteAsync(buffer, 0, buffer.Length,ct);
            }
        }

        private async Task<bool> SendJoinChannel(int channelId,string token, CancellationToken ct)
        {
            
            var packetModel = new {roomid = channelId, uid = 0, protover = 3, key=token, platform="danmuji", type=2};


            var playload = JsonConvert.SerializeObject(packetModel);
             await SendSocketDataAsync(7, playload,ct);
            return true;
        }

     

        public DanmakuLoader()
        {
        }


    }

    public delegate void LogMessageEvt(object sender, LogMessageArgs e);
    public class LogMessageArgs
    {
        public string message = string.Empty;
    }


    public struct DanmakuProtocol
    {
        /// <summary>
        /// ��Ϣ�ܳ��� (Э��ͷ + ���ݳ���)
        /// </summary>
        public int PacketLength;
        /// <summary>
        /// ��Ϣͷ���� (�̶�Ϊ16[sizeof(DanmakuProtocol)])
        /// </summary>
        public short HeaderLength;
        /// <summary>
        /// ��Ϣ�汾��
        /// </summary>
        public short Version;
        /// <summary>
        /// ��Ϣ����
        /// </summary>
        public int Action;
        /// <summary>
        /// ����, �̶�Ϊ1
        /// </summary>
        public int Parameter;

        public static DanmakuProtocol FromBuffer(byte[] buffer)
        {
            if (buffer.Length < 16) { throw new ArgumentException();}
            return new DanmakuProtocol()
            {
                PacketLength = EndianBitConverter.BigEndian.ToInt32(buffer,0),
                HeaderLength = EndianBitConverter.BigEndian.ToInt16(buffer, 4),
                Version = EndianBitConverter.BigEndian.ToInt16(buffer, 6),
                Action = EndianBitConverter.BigEndian.ToInt32(buffer, 8),
                Parameter = EndianBitConverter.BigEndian.ToInt32(buffer, 12),
            };
        }
    }

}