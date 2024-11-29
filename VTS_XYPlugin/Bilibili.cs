namespace VTS_XYPlugin
{
    public abstract class Bilibili : RegistrableSingleton<Bilibili>
    {
        // 是否允许连接B站，当启动参数里含有nobili的时候，不连接B站
        public static bool CanConnectBili = true;

        public void Init()
        {
            if (XYPlugin.CmdArgs.Contains("-nobili"))
            {
                CanConnectBili = false;
                XYLog.LogMessage($"当前已禁用连接Bilibili");
            }
            if (CanConnectBili)
            {
                InternalInit();
            }
        }

        internal virtual void InternalInit()
        {

        }

        public abstract void Update();

        public abstract void Connect(string host);
    }
}