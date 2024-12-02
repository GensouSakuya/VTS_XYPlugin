# VTS_XYPlugin
用于Bilibili的VTube Studio的插件

主要功能: B站直播礼物掉落

基于BepInEx 6.0.0-pre.1 Mono x64

连接B站使用的弹幕机: https://play-live.bilibili.com/details/1651388990835

我的B站主页: https://space.bilibili.com/1306433

## 下载
[releases][1]

[1]:https://github.com/xiaoye97/VTS_XYPlugin/releases

## 开发
添加环境变量
- VTSPath，值为Vtube Studio的路径，如"C:\Program Files (x86)\Steam\steamapps\common\VTube Studio"


## ps
因为不会改unity，所以在XYPluginConfig目录下新增了一个GlobalConfig.ext.json用来储存新增的配置
新增配置如下
- BilibiliDanmakuSource: 弹幕来源类型，0为默认弹幕姬，1为blivechat插件
- DanmakuServiceHost: 弹幕来源服务地址，默认"127.0.0.1:9000"，是弹幕姬默认地址