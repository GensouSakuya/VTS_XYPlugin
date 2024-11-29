using SuperSimpleTcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using VTS_XYPlugin_Common;

namespace VTS_XYPlugin.Danmaku
{
    public class XYDanMuShare:Bilibili
    {
        private SimpleTcpClient client;
        private BLiveDanmuParser danmuParser;
        private string _host;

        private static float reConnectCD = 120;

        internal override void InternalInit()
        {
            if (XYPlugin.CmdArgs.Contains("-nobili"))
            {
                CanConnectBili = false;
                XYLog.LogMessage($"当前已禁用连接Bilibili");
            }
            if (CanConnectBili)
            {
                danmuParser = new BLiveDanmuParser();
                danmuParser.OnDanmaku += DanmuParser_OnDanmaku;
                danmuParser.OnGift += DanmuParser_OnGift;
                danmuParser.OnGuardBuy += DanmuParser_OnGuardBuy;
                danmuParser.OnSuperChat += DanmuParser_OnSuperChat;
            }
        }

        public override void Update()
        {
            if (CanConnectBili)
            {
                if (!client.IsConnected)
                {
                    reConnectCD -= Time.deltaTime;
                    if (reConnectCD < 0)
                    {
                        reConnectCD = 120;
                        XYLog.LogMessage($"开始尝试重连弹幕机");
                        Connect(_host);
                    }
                }
            }
        }

        public override void Connect(string host)
        {
            _host = host;
            client = new SimpleTcpClient(host);
            client.Events.Connected += Events_Connected;
            client.Events.Disconnected += Events_Disconnected;
            client.Events.DataReceived += Events_DataReceived;
            client.Connect();
        }

        private void Events_DataReceived(object sender, DataReceivedEventArgs e)
        {
            string data = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
            if (data.StartsWith("XYDanMuShareForCopyLiuDanMuJi;"))
            {
                var rawData = data.Replace("XYDanMuShareForCopyLiuDanMuJi;", "");
                danmuParser.ProcessNotice(rawData);
            }
        }

        private void Events_Disconnected(object sender, ConnectionEventArgs e)
        {
            XYLog.LogMessage($"与弹幕广播的连接已断开，即将尝试重连。");
            reConnectCD = 1f;
            client = null;
        }

        private void Events_Connected(object sender, ConnectionEventArgs e)
        {
            XYLog.LogMessage($"已连接到弹幕广播");
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
