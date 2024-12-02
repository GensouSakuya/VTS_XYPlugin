using Newtonsoft.Json;

namespace BlivechatPlugin
{
    public abstract class BaseBLivechatMessage<T>
    {
        [JsonProperty("cmd")]
        public abstract int Command { get; }

        [JsonProperty("data")]
        public T Data { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class HeartbeatBliveMessage : BaseBLivechatMessage<object>
    {
        public static readonly HeartbeatBliveMessage Singleton = new HeartbeatBliveMessage();

        public override int Command => 0;
    }
}
