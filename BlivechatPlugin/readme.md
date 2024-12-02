## 发布
Linux版
使用Dist.pubxml发布后直接将产生的VTS_XYPlugin目录复制到blivechat的data/plugins目录下即可

其它平台需要手动修改目标平台，并修改plugin.json中run字段的启动命令

## ws测试数据

测试上舰
test{
    "cmd":"LIVE_OPEN_PLATFORM_GUARD",
    "data":{
        "user_info":{
                "uid":123,
                "uname":"test"
            },
        "guard_level":1,
        "guard_num":1
    }
}

测试礼物
test{
    "cmd":"LIVE_OPEN_PLATFORM_SEND_GIFT",
    "data":{
        "uid":123,
        "uname":"tset",
        "gift_name":"233",
        "gift_num":1,
        "paid":true,
        "price":123,
        "uface":"111"
    }
}