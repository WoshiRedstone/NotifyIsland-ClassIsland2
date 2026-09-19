# NotifyIsland（重构）
- 源项目[NotifyIsland](https://github.com/DeepslateQAQ/NotifyIsland)

- 由于代码复杂度原因，该项目的命名空间保持原项目

用于将ClassIsland的提醒功能更好的和第三方通知服务集成,以及PutIsland同款网络文本。

适用于 ClassIsland **2.0+**

## 使用方式

安装此插件，在设置中调整需要修改的内容，将 **【启用 NotifyIsland 服务】开关** 打开即可。

**开启服务并调试完毕后，务必在展开的菜单中设置访问密钥！！！**

默认监听 `localhost:1379`。`localhost` 无需管理员权限；若改为 `*` 或 `+`（监听所有网卡）则需要以管理员身份运行 ClassIsland。

## 功能一：HTTP 触发提醒

通过请求 `/api/notify` 接口，可以触发 ClassIsland 提醒。

（示例：发送一个标题为“提醒标题”持续3秒，内容为“提醒内容”持续15秒的提醒，并自定义提醒语音内容）

**自定义语音提醒内容可以省略，省略后将使用显示文本。**

```
POST /api/notify HTTP/1.1
Authorization: Bearer 1145141919
Content-Type: application/json

{
    "title": "提醒标题",
    "title_duration": 3,
    "title_voice": "这是语音播放的提醒标题",
    "content": "提醒内容",
    "content_duration": 15,
    "content_voice": "这是语音播放的提醒内容：哼哼哼啊啊啊啊啊",
    "effect_enabled": true,
    "sound_enabled": true
}
```

```
HTTP/1.1 200 OK
Content-Type: application/json

{"Success":true,"Code":200,"Status":200,"Message":"[200]已推送到ClassIsland"}
```

### 错误码

| HTTP | Code | Message |
| --- | --- | --- |
| 200 | 200 | `[200]已推送到ClassIsland` |
| 400 | -400 | `传入内容应为JSON格式` |
| 401 | -401 | `API端点需要认证` |
| 403 | -403 | `API端点认证失败` |
| 405 | -405 | `API端点仅允许POST请求` |

## 功能二：HTTP 文本组件

在主界面添加 **【HTTP 文本】** 组件，并在组件设置中填入一个 **组件 ID**，即可通过 HTTP 请求更新它的显示内容。用法与 [PutIsland](https://github.com/pizeroLOL/PutIsland) 一致：

```
POST /<组件ID> HTTP/1.1
Content-Type: text/plain

这里是新的文本内容
```

- 请求体即文本内容（UTF-8）
- 也支持 `/api/text/<组件ID>` 的写法，避免与其它路径冲突
- **同一个组件 ID 可以被多个组件同时使用**，会一起更新
- 组件的文本会随 ClassIsland 配置一起保存，重启后仍保留

响应：

```
HTTP/1.1 200 OK
Content-Type: application/json

{"Success":true,"Code":200,"Status":200,"Message":"[200]已更新文本组件（更新了 1 个组件）"}
```

| HTTP | 说明 |
| --- | --- |
| 200 | 更新成功 |
| 400 | 路径为空（`没有 Token`） |
| 401 | 开启了「文本接口也要求密钥」但未携带正确密钥 |
| 404 | 没有组件使用该 ID |

> 提示：访问 `GET /` 可以查看服务状态以及当前已注册的组件 ID 列表，便于调试。

## 接口一览

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | `/api/notify` | 触发提醒（JSON） |
| POST | `/<组件ID>` | 更新文本组件（纯文本，PutIsland 兼容） |
| POST | `/api/text/<组件ID>` | 同上，命名空间形式 |
| GET | `/` | 服务状态与已注册组件 ID |

## 隐私与安全

- 建议仅监听 `localhost`，只允许本机软件调用。
- 提醒接口始终支持 Bearer 密钥校验；文本接口默认不校验（保持 PutIsland 用法），如需校验请在设置中打开「文本接口也要求密钥」。


## 许可证
[GNU General Public License v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html)
<img width="127" height="51" alt="image" src="https://github.com/user-attachments/assets/f3f78e8e-4940-494e-9430-871a7a2cbd64" />

## 特别鸣谢
[Deepseek](https://deepseek.com)

[GLM](https://open.bigmodel.cn/)

[PutIsland](https://github.com/pizeroLOL/PutIsland)


