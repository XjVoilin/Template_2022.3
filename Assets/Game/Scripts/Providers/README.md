# Providers 层

框架扩展层，存放JulyGF框架Provider的自定义实现。

## 设计原则

1. **实现框架接口**：继承ProviderBase，实现IXxxProvider接口
2. **技术实现**：只负责技术实现，不包含业务逻辑
3. **可替换**：通过DI注册，便于替换不同实现

## 目录结构

```
Providers/
├── Resource/      # 资源加载（YooAsset）
├── Config/        # 配置表（Luban）
├── Network/       # 网络通信（WebSocket）
├── Analytics/     # 数据统计（TalkingData）
├── HotUpdate/     # 热更新（HybridCLR）
└── Serialize/     # 序列化/加密
```

## 注册方式

在GameEntry.RegisterProviders()中注册：
```csharp
RegisterProvider<IResourceProvider, YooAssetResourceProvider>();
RegisterProvider<IConfigProvider, LubanConfigProvider>();
```
