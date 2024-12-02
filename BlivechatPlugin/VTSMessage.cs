using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlivechatPlugin
{
    public abstract class BaseVTSMessage
    {
        [JsonProperty("cmd")]
        public abstract string Command { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public interface IGeneratableByJObj<T>
    {
        static abstract T GenerateByJObj(JObject jobj);
    }

    public abstract class VTSMessage<T>: BaseVTSMessage
    {
        [JsonProperty("data")]
        public T Data { get; set; }
    }

    public class VTSGiftMessage : VTSMessage<VTSGiftBody>, IGeneratableByJObj<VTSGiftMessage>
    {
        public override string Command => "LIVE_OPEN_PLATFORM_SEND_GIFT";

        public static VTSGiftMessage GenerateByJObj(JObject jobj)
        {
            var data = jobj["data"].ToArray();

            //'id': uuid.uuid4().hex,
            //'avatarUrl': avatar_url,
            //'timestamp': message.timestamp,
            //'authorName': message.uname,
            //'totalCoin': 0 if not is_paid_gift else message.total_coin,
            //'totalFreeCoin': 0 if is_paid_gift else message.total_coin,
            //'giftName': message.gift_name,
            //'num': message.num,
            //'giftId': message.gift_id,
            //'giftIconUrl': '',
            //'uid': str(message.uid) if message.uid != 0 else message.uname,
            //'privilegeType': message.guard_level,
            //'medalLevel': 0,
            //'medalName': '',
            var msg = new VTSGiftMessage
            {
                Data = new VTSGiftBody
                {
                    UserFace = data[1].Value<string>(),
                    UserName = data[3].Value<string>(),
                    Paid = data[4].Value<int>() > 0,
                    Price = data[4].Value<int>(),
                    GiftName = data[6].Value<string>(),
                    GiftNum = data[7].Value<int>(),
                    OpenId = data[10].Value<string>(),
                }
            };
            return msg;
        }
    }

    public class VTSGiftBody
    {
        [JsonProperty("uid")]
        public long Uid;
        [JsonProperty("open_id")]
        public string OpenId;
        [JsonProperty("uname")]
        public string UserName;
        [JsonProperty("uface")]
        public string UserFace;
        [JsonProperty("gift_name")]
        public string GiftName;
        [JsonProperty("gift_num")]
        public long GiftNum;
        [JsonProperty("price")]
        public long Price;
        [JsonProperty("paid")]
        public bool Paid;
    }

    public class VTSGuardMessage : VTSMessage<VTSGuardBody>, IGeneratableByJObj<VTSGuardMessage>
    {
        public override string Command => "LIVE_OPEN_PLATFORM_GUARD";

        public static VTSGuardMessage GenerateByJObj(JObject jobj)
        {
            var data = jobj["data"].ToArray();
            //'id': uuid.uuid4().hex,
            //'avatarUrl': avatar_url,
            //'timestamp': message.start_time,
            //'authorName': message.username,
            //'privilegeType': message.guard_level,
            //'num': message.num,
            //'unit': '月',  # 单位在USER_TOAST_MSG消息里，不想改消息。现在没有别的单位，web接口也很少有人用了，先写死吧
            //'total_coin': message.price* message.num,
            //'uid': str(message.uid) if message.uid != 0 else message.username,
            //'medalLevel': 0,
            //'medalName': '',
            var msg = new VTSGuardMessage
            {
                Data = new VTSGuardBody
                {
                    UserInfo = new VTSUserInfoBody
                    {
                        OpenId = data[8].Value<string>(),
                        UserFace = data[1].Value<string>(),
                        UserName = data[3].Value<string>(),
                    },
                    GuardLevel = data[4].Value<int>(),
                    GuardNum = data[5].Value<int>(),
                }
            };
            return msg;
        }
    }

    public class VTSGuardBody
    {
        [JsonProperty("guard_level")]
        public long GuardLevel;
        [JsonProperty("guard_num")]
        public long GuardNum;
        [JsonProperty("user_info")]
        public VTSUserInfoBody UserInfo;
    }

    public class VTSUserInfoBody
    {
        [JsonProperty("uid")]
        public long Uid;
        [JsonProperty("open_id")]
        public string OpenId;
        [JsonProperty("uname")]
        public string UserName;
        [JsonProperty("uface")]
        public string UserFace;
    }
}
