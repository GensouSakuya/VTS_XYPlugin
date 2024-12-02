using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using Websocket.Client;

namespace BlivechatPlugin
{
    public class DanmakuProxy
    {
        private ILogger _logger;
        private IWebsocketClient _wsClient;
        private readonly List<WebSocket> _clients = new();
        private CancellationTokenSource _heartbeatCancellationToken = new CancellationTokenSource();

        public DanmakuProxy(IWebsocketClient wsClient, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DanmakuProxy>();
            _wsClient = wsClient;
            _wsClient.MessageReceived.Subscribe(async msg => await HandleBlivechatMsg(msg));
            _wsClient.DisconnectionHappened.Subscribe(async di =>
            {
                _logger.LogError(di.Exception, "disconnected, trying to reconnect");
                await _wsClient.Reconnect();
            });
        }

        public async Task Start()
        {
            await _wsClient.StartOrFail();
            _ = Task.Run(Heartbeat);
        }

        private async Task Heartbeat()
        {
            while (!_heartbeatCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _wsClient.SendInstant(HeartbeatBliveMessage.Singleton.ToString());
                }
                catch(Exception e)
                {
                    _logger.LogError(e, "heartbeat failed");
                }
                finally
                {
                    await Task.Delay(30000, _heartbeatCancellationToken.Token);
                }
            }
        }

        public async Task AddAsClient(WebSocket client)
        {
            _clients.Add(client);
            var buffer = new byte[1024 * 2];
            var sendBuffer = new byte[1024 * 2];
            try
            {
                while (client.State == WebSocketState.Open)
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), default);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, default);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var rawMessage = Encoding.UTF8.GetString(buffer[..result.Count]);
                        _logger.LogInformation("reveiced from client: {0}", rawMessage);
                        //ignore
                        if (rawMessage.StartsWith("test"))
                        {
                            //测试用，把消息转发出去
                            rawMessage = rawMessage.Substring(4, rawMessage.Length - 4);
                            var testlength = Encoding.UTF8.GetBytes(rawMessage, new ArraySegment<byte>(buffer));
                            foreach (var s in _clients)
                            {
                                if (s == client)
                                    continue;
                                if (s.State == WebSocketState.Open)
                                {
                                    await s.SendAsync(buffer[..testlength], WebSocketMessageType.Text, true, default);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ws connection error");
            }
            _clients.Remove(client);
        }

        public async Task HandleBlivechatMsg(ResponseMessage msg)
        {
            try
            {
                var rawMsg = msg.Text;
                _logger.LogInformation("received from blivechat: {0}", rawMsg);
                if (!string.IsNullOrWhiteSpace(rawMsg) && _clients.Count > 0)
                {
                    var handler = new BlivechatMessageHandler(rawMsg);
                    if (!handler.CanHandle)
                        return;

                    var giftMsg = handler.ConvertToVTSMessage().ToString();
                    var length = Encoding.UTF8.GetByteCount(giftMsg);
                    var memory = MemoryPool<byte>.Shared.Rent(length);
                    Encoding.UTF8.GetBytes(giftMsg, memory.Memory.Span);
                    foreach (var s in _clients)
                    {
                        if (s.State == WebSocketState.Open)
                        {
                            await s.SendAsync(memory.Memory[..length], WebSocketMessageType.Text, true, default);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                _logger.LogError(e, "handle danmaku msg error");
            }
        }
    }
}
