# Worktile message monitor

使用 [SocketIOClient](https://github.com/doghappy/socket.io-client-csharp) 作为 Socket.IO 客户端，监控 Worktile 的消息，在使用之前，需要配置登录信息。

config.json

```json
{
  "domain": "at",
  "user": "xxx",
  "password": "password",
  "teamId": "567b66f417986913404da9ff"
}

```

此配置的值可以通过浏览器登录 Worktile 获取到。

```
dotnet Worktile.MessageMonitor.dll >> 2020-5-16.log
```

此款工具主要是为了测试 SocketIOClient 的稳定性。
