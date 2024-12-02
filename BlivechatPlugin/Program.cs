// See https://aka.ms/new-console-template for more information

using BlivechatPlugin;
using RestSharp;
using System.Net.WebSockets;
using Websocket.Client;

var blcPort = int.TryParse(Environment.GetEnvironmentVariable("BLC_PORT"), out var port) ? port : 80;
var blcToken = Environment.GetEnvironmentVariable("BLC_TOKEN");
var blcHttpUrl = $"http://localhost:{blcPort}";
var blcWsUrl = $"ws://localhost:{blcPort}/api/plugin/websocket";
var restClient = new RestClient();
restClient.AddDefaultHeader("Authorization", $"Bearer {blcToken}");

var websocketClient = new WebsocketClient(new Uri(blcWsUrl), () => {
    var nativeClient = new ClientWebSocket();
    nativeClient.Options.SetRequestHeader("Authorization", $"Bearer {blcToken}");
    return nativeClient;
});

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
builder.Services.AddSingleton<IWebsocketClient>(websocketClient);
builder.Services.AddSingleton<DanmakuProxy>();
var app = builder.Build();
app.UseWebSockets();
app.MapGet("/", async (HttpContext context, DanmakuProxy proxy, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("MapGet");
    if (context.WebSockets.IsWebSocketRequest)
    {
        logger.LogInformation("client connected");
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await proxy.AddAsClient(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected a WebSocket request");
    }
});
var proxy = app.Services.GetService<DanmakuProxy>();
await proxy.Start();
await app.RunAsync("http://0.0.0.0:7777");