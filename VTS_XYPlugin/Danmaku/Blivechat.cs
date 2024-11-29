using System;
using System.Threading.Tasks;
using UnityEngine;
using VTS_XYPlugin_Common;
using Websocket.Client;

namespace VTS_XYPlugin.Danmaku
{
    public class Blivechat : Bilibili
    {
        private string _host;
        private WebsocketClient client;
        private BLiveDanmuParser danmuParser;

        private static float reConnectCD = 120;

        internal override void InternalInit()
        {
            danmuParser = new BLiveDanmuParser();
            danmuParser.OnDanmaku += DanmuParser_OnDanmaku;
            danmuParser.OnGift += DanmuParser_OnGift;
            danmuParser.OnGuardBuy += DanmuParser_OnGuardBuy;
            danmuParser.OnSuperChat += DanmuParser_OnSuperChat;
        }

        public override void Update()
        {
            if (CanConnectBili)
            {
                if (!client.IsRunning)
                {
                    reConnectCD -= Time.deltaTime;
                    if (reConnectCD < 0)
                    {
                        reConnectCD = 120;
                        XYLog.LogMessage($"开始尝试重连弹幕机");
                        _ = client.Stop(System.Net.WebSockets.WebSocketCloseStatus.Empty, "client stop");
                        Connect(_host);
                    }
                }
            }
        }

        public override void Connect(string host)
        {
            XYLog.LogMessage($"连接Blivechat:{host}");
            _host = host;
            var uri = new Uri($"ws://{host}");
            client = new WebsocketClient(uri);
            client.MessageReceived.Subscribe(async (msg) => await Events_DataReceived(msg));
            client.DisconnectionHappened.Subscribe(async d => await Events_Disconnected(d));
            _ = client.Start();
            XYLog.LogMessage($"已连接到弹幕广播");
        }

        private Task Events_DataReceived(ResponseMessage e)
        {
            string data = e.Text;
            danmuParser.ProcessNotice(data);
            return Task.CompletedTask;
        }

        private Task Events_Disconnected(DisconnectionInfo e)
        {
            XYLog.LogMessage($"与弹幕广播的连接已断开，即将尝试重连。");
            reConnectCD = 1f;
            client = null;
            return Task.CompletedTask;
        }

        private void DanmuParser_OnGift(OpenBLive.Runtime.Data.SendGift sendGift)
        {
            var message = new BGiftMessage()
            {
                用户ID = sendGift.uid.ToString(),
                用户名 = sendGift.userName,
                礼物名 = sendGift.giftName,
                礼物数量 = (int)sendGift.giftNum,
                瓜子类型 = sendGift.paid ? BGiftCoinType.金瓜子 : BGiftCoinType.银瓜子,
                瓜子数量 = (int)sendGift.price,
                头像图片链接 = sendGift.userFace
            };
            BilibiliHeadCache.Instance.OnRecvGift(message);
            MessageCenter.Instance.Send(message);
        }

        private void DanmuParser_OnDanmaku(OpenBLive.Runtime.Data.Dm dm)
        {
            var message = new BDanMuMessage()
            {
                用户ID = dm.uid.ToString(),
                用户名 = dm.userName,
                舰队类型 = dm.guardLevel.ToString().ToJianDuiType(),
                粉丝牌名称 = dm.fansMedalName,
                粉丝牌等级 = (int)dm.fansMedalLevel,
                弹幕 = dm.msg
            };
            MessageCenter.Instance.Send(message);
        }

        private void DanmuParser_OnSuperChat(OpenBLive.Runtime.Data.SuperChat sc)
        {
            var message = new BSCMessage()
            {
                用户ID = sc.uid.ToString(),
                用户名 = sc.userName,
                金额 = (int)sc.rmb,
                持续时间 = (int)(sc.endTime - sc.startTime),
                SC = sc.message
            };
            MessageCenter.Instance.Send(message);
        }

        private void DanmuParser_OnGuardBuy(OpenBLive.Runtime.Data.Guard guard)
        {
            var message = new BBuyJianDuiMessage()
            {
                用户ID = guard.userInfo.uid.ToString(),
                用户名 = guard.userInfo.userName,
                开通类型 = guard.guardLevel.ToString().ToJianDuiType(),
                开通数量 = (int)guard.guardNum,
            };
            MessageCenter.Instance.Send(message);
        }
    }
}
