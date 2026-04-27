using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Upyun.Models;
using Xunit.Abstractions;

namespace Upyun.Test;

public sealed class UpyunClientTests
{
    private const string Endpoint = "https://v0.api.upyun.com";
    private const string TestRoot = "/upyun-sdk-tests";
    private static readonly Lazy<IConfigurationRoot> Secrets = new(CreateConfiguration);
    private readonly ITestOutputHelper _output;

    public UpyunClientTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RestApi_AllOperations_WorkInRealEnvironment()
    {
        _output.WriteLine("正在读取测试配置...");
        UpyunClient client = CreateClient();
        string testId = Guid.NewGuid().ToString("N");
        string directoryPath = $"{TestRoot}/{testId}";
        string sourcePath = $"{directoryPath}/source.txt";
        string copiedPath = $"{directoryPath}/copied.txt";
        string movedPath = $"{directoryPath}/moved.txt";
        string missingPath = $"{directoryPath}/missing.txt";
        byte[] content = Encoding.UTF8.GetBytes($"hello upyun sdk {testId}");
        bool movedDeleted = false;
        bool copiedDeleted = false;
        bool sourceDeleted = false;
        bool directoryDeleted = false;

        try
        {
            _output.WriteLine($"正在创建测试根目录：{TestRoot}...");
            await client.CreateDirectoryAsync(TestRoot);

            _output.WriteLine($"正在创建测试目录：{directoryPath}...");
            await client.CreateDirectoryAsync(directoryPath);

            _output.WriteLine($"正在获取目录信息：{directoryPath}...");
            UpyunFileInfo directoryInfo = await client.GetFileInfoAsync(directoryPath);
            Assert.Equal("folder", directoryInfo.Type);

            _output.WriteLine($"正在上传文件：{sourcePath}...");
            await client.UploadFileAsync(
                sourcePath,
                content,
                "text/plain",
                headers: new Dictionary<string, string>
                {
                    { "x-upyun-meta-sdk-test", testId }
                });

            _output.WriteLine($"正在获取文件信息：{sourcePath}...");
            UpyunFileInfo fileInfo = await client.GetFileInfoAsync(sourcePath);
            Assert.Equal("file", fileInfo.Type);
            Assert.Equal(content.Length, fileInfo.Size);

            _output.WriteLine($"正在下载文件到字节数组：{sourcePath}...");
            byte[] downloaded = await client.DownloadFileAsync(sourcePath);
            Assert.Equal(content, downloaded);

            _output.WriteLine($"正在下载文件到流：{sourcePath}...");
            using MemoryStream destination = new();
            await client.DownloadFileAsync(sourcePath, destination);
            Assert.Equal(content, destination.ToArray());
            
            await Task.Delay(3000);
            
            _output.WriteLine($"正在复制文件：{sourcePath} -> {copiedPath}...");
            await client.CopyFileAsync(sourcePath, copiedPath);
            
            _output.WriteLine($"正在读取复制后的文件：{copiedPath}...");
            byte[] copied = await client.DownloadFileAsync(copiedPath);
            Assert.Equal(content, copied);

            _output.WriteLine($"正在移动文件：{sourcePath} -> {movedPath}...");
            await client.MoveFileAsync(sourcePath, movedPath);
            sourceDeleted = true;

            _output.WriteLine($"正在读取移动后的文件：{movedPath}...");
            byte[] moved = await client.DownloadFileAsync(movedPath);
            Assert.Equal(content, moved);

            _output.WriteLine($"正在获取目录文件列表：{directoryPath}...");
            UpyunDirectoryList directoryList = await client.GetDirectoryListAsync(directoryPath, limit: 100);
            Assert.Contains(directoryList.Files, item => item.Name == "copied.txt");
            Assert.Contains(directoryList.Files, item => item.Name == "moved.txt");

            _output.WriteLine("正在获取服务使用量...");
            long usage = await client.GetUsageAsync();
            Assert.True(usage >= content.Length);

            _output.WriteLine($"正在验证下载不存在文件会抛出异常：{missingPath}...");
            UpyunException exception = await Assert.ThrowsAsync<UpyunException>(() => client.DownloadFileAsync(missingPath));
            Assert.NotNull(exception.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, exception.StatusCode);

            _output.WriteLine($"正在删除移动后的文件：{movedPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteFileAsync(movedPath);
            movedDeleted = true;

            _output.WriteLine($"正在删除复制后的文件：{copiedPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteFileAsync(copiedPath);
            copiedDeleted = true;
            sourceDeleted = true;

            _output.WriteLine($"正在删除测试目录：{directoryPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteFileAsync(directoryPath);
            directoryDeleted = true;
        }
        finally
        {
            if (!movedDeleted)
            {
                await DeleteIfExistsAsync(client, movedPath, "移动后的文件");
            }

            if (!copiedDeleted)
            {
                await DeleteIfExistsAsync(client, copiedPath, "复制后的文件");
            }

            if (!sourceDeleted)
            {
                await DeleteIfExistsAsync(client, sourcePath, "源文件");
            }

            if (!directoryDeleted)
            {
                await DeleteIfExistsAsync(client, directoryPath, "测试目录");
            }
        }
    }

    private static UpyunClient CreateClient()
    {
        return new UpyunClient(
            GetRequiredSecret("Bucket"),
            GetRequiredSecret("OperatorName"),
            GetRequiredSecret("Password"),
            Endpoint);
    }

    private static IConfigurationRoot CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets<UpyunClientTests>()
            .Build();
    }

    private static string GetRequiredSecret(string key)
    {
        string? value = Secrets.Value[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请在 Upyun.Test 的 User Secrets 中配置 {key}。");
        }

        return value;
    }

    private async Task DeleteIfExistsAsync(UpyunClient client, string path, string name)
    {
        try
        {
            _output.WriteLine($"正在兜底清理{name}：{path}...");
            await client.DeleteFileAsync(path);
        }
        catch (UpyunException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _output.WriteLine($"兜底清理{name}时目标不存在：{path}");
        }
        catch (UpyunException)
        {
            // 兜底清理不参与测试断言，避免掩盖主流程失败原因。
            _output.WriteLine($"兜底清理{name}失败，已忽略：{path}");
        }
    }
}
