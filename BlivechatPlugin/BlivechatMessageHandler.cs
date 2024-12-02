using Newtonsoft.Json.Linq;

namespace BlivechatPlugin
{
    public class BlivechatMessageHandler
    {
        private static int[] _handleCmds = new[]
        {
            51,//礼物
            52,//上舰
            //50//测试用
        };

        public string Raw { get; private set; }

        private JObject _jObj;

        private Lazy<int> _cmd;

        public BlivechatMessageHandler(string rawMessage)
        {
            Raw = rawMessage;
            _jObj = JObject.Parse(Raw);
            _cmd = new Lazy<int>(() => _jObj["cmd"]?.Value<int>() ?? -1);
        }

        public bool CanHandle => _handleCmds.Contains(_cmd.Value);

        public BaseVTSMessage ConvertToVTSMessage()
        {
            BaseVTSMessage vtsMessage = _cmd.Value switch
            {
                51 => VTSGiftMessage.GenerateByJObj(_jObj),
                52 => VTSGuardMessage.GenerateByJObj(_jObj),
                //50 => _jObj["data"][4].Value<string>() == "test" ? new VTSGiftMessage
                //{
                //    Data = new VTSGiftBody { GiftName = "233",
                //        GiftNum = 1,
                //        OpenId = "fwaefeawf",
                //        Paid = true,
                //        Price = 123,
                //        UserName = "test" }
                //} : throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            }; ;
            return vtsMessage;
        }
    }
}
