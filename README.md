# Upyun C# SDK

又拍云 REST API 的 C# 异步客户端，支持文件上传、下载、复制、移动、删除，目录创建与列表查询，以及空间用量查询。

> 本项目由 AI 开发。

## 支持的目标框架

- .NET 6.0
- .NET Standard 2.0

## 安装

如果已发布到 NuGet，可以通过包管理器安装：

```bash
dotnet add package Upyun
```

如果使用源码开发，可以直接引用项目：

```bash
dotnet add reference ./Upyun/Upyun.csproj
```

## 快速开始

```csharp
using Upyun;

using var client = new UpyunClient(
    bucket: "your-bucket",
    operatorName: "your-operator",
    password: "your-password");

await client.UploadFileAsync(
    path: "/images/hello.txt",
    content: System.Text.Encoding.UTF8.GetBytes("hello upyun"),
    contentType: "text/plain");

byte[] fileBytes = await client.DownloadFileAsync("/images/hello.txt");
```

`bucket` 为又拍云服务名称，`operatorName` 和 `password` 为服务操作员名称与密码。默认接入点为 `https://v0.api.upyun.com`，如需自定义可以在构造函数中传入 `endpoint`。

## 使用已有 HttpClient

在 ASP.NET Core 等长期运行的应用中，建议复用 `HttpClient`：

```csharp
using System.Net.Http;
using Upyun;

HttpClient httpClient = new HttpClient();
var client = new UpyunClient(
    bucket: "your-bucket",
    operatorName: "your-operator",
    password: "your-password",
    httpClient: httpClient);
```

通过该构造函数传入的 `HttpClient` 不会被 `UpyunClient.Dispose()` 释放，生命周期由调用方管理。

## 在 ASP.NET Core 中使用 AddHttpClient

可以通过 `IHttpClientFactory` 管理 `HttpClient` 生命周期，并把 `UpyunClient` 注册为服务：

```csharp
using Upyun;

builder.Services.AddHttpClient<UpyunClient>((serviceProvider, httpClient) =>
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();

    return new UpyunClient(
        bucket: configuration["Upyun:Bucket"],
        operatorName: configuration["Upyun:OperatorName"],
        password: configuration["Upyun:Password"],
        httpClient: httpClient);
});
```

配置示例：

```json
{
  "Upyun": {
    "Bucket": "your-bucket",
    "OperatorName": "your-operator",
    "Password": "your-password"
  }
}
```

之后即可通过依赖注入使用：

```csharp
public sealed class FileService
{
    private readonly UpyunClient _client;

    public FileService(UpyunClient client)
    {
        _client = client;
    }

    public Task UploadAsync(byte[] content)
    {
        return _client.UploadFileAsync("/docs/readme.txt", content, "text/plain");
    }
}
```

## 上传文件

上传本地文件：

```csharp
await client.UploadFileAsync(
    path: "/backup/logo.png",
    localFilePath: "./Logo.png",
    contentType: "image/png");
```

上传字节数组并附加自定义 Header：

```csharp
await client.UploadFileAsync(
    path: "/docs/readme.txt",
    content: System.Text.Encoding.UTF8.GetBytes("hello"),
    contentType: "text/plain",
    headers: new Dictionary<string, string>
    {
        { "x-upyun-meta-source", "sdk" }
    });
```

上传流：

```csharp
using FileStream stream = File.OpenRead("./video.mp4");

await client.UploadFileAsync(
    path: "/videos/video.mp4",
    content: stream,
    contentLength: stream.Length,
    contentType: "video/mp4");
```

当流不可定位时，必须显式传入 `contentLength`。

## 下载文件

下载到字节数组：

```csharp
byte[] content = await client.DownloadFileAsync("/docs/readme.txt");
```

下载到指定流：

```csharp
using FileStream output = File.Create("./readme.txt");
await client.DownloadFileAsync("/docs/readme.txt", output);
```

## 文件管理

```csharp
await client.CopyFileAsync(
    sourcePath: "/docs/readme.txt",
    destinationPath: "/docs/readme-copy.txt");

await client.MoveFileAsync(
    sourcePath: "/docs/readme-copy.txt",
    destinationPath: "/docs/readme-moved.txt");

await client.DeleteFileAsync("/docs/readme-moved.txt");
```

## 目录操作

创建目录：

```csharp
await client.CreateDirectoryAsync("/docs");
```

删除空目录：

```csharp
await client.DeleteDirectoryAsync("/docs/empty-folder");
```

又拍云只允许删除空目录，非空目录需要先删除里面的文件或子目录。

获取文件或目录信息：

```csharp
using Upyun.Models;

UpyunFileInfo info = await client.GetFileInfoAsync("/docs/readme.txt");

Console.WriteLine(info.Type);              // file 或 folder
Console.WriteLine(info.Size);              // 文件大小，单位：字节
Console.WriteLine(info.CreatedAtUnixTime); // 秒级 Unix 时间戳
Console.WriteLine(info.ContentMd5);
```

分页列出目录：

```csharp
using Upyun.Models;

string iter = null;
bool isEnd;

do
{
    UpyunDirectoryList list = await client.GetDirectoryListAsync(
        path: "/docs",
        iter: iter,
        limit: 100,
        order: UpyunListOrder.Asc);

    foreach (UpyunFileSystem item in list.Files)
    {
        if (item is UpyunDirectory directory)
        {
            Console.WriteLine($"DIR\t{directory.LastModifiedTime:O}\t{directory.Name}");
        }
        else if (item is UpyunFile file)
        {
            Console.WriteLine($"FILE\t{file.Type}\t{file.Length}\t{file.LastModifiedTime:O}\t{file.Name}");
        }
    }

    iter = list.Iter;
    isEnd = list.IsEnd;
}
while (!isEnd);
```

## 查询空间用量

```csharp
long usageBytes = await client.GetUsageAsync();
Console.WriteLine($"Usage: {usageBytes} bytes");
```

## 错误处理

当又拍云返回非成功状态码，或响应内容无法解析时，SDK 会抛出 `UpyunException`：

```csharp
try
{
    await client.DownloadFileAsync("/not-found.txt");
}
catch (UpyunException exception)
{
    Console.WriteLine(exception.StatusCode);
    Console.WriteLine(exception.ResponseContent);
}
```

## 运行测试

测试项目会访问真实又拍云服务。运行前需要为 `Upyun.Test` 配置 User Secrets：

```bash
dotnet user-secrets set "Bucket" "your-bucket" --project ./Upyun.Test
dotnet user-secrets set "OperatorName" "your-operator" --project ./Upyun.Test
dotnet user-secrets set "Password" "your-password" --project ./Upyun.Test
dotnet test
```

测试会在服务中创建 `/upyun-sdk-tests` 下的临时目录和文件，并在测试结束时清理。
