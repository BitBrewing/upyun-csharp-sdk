using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Upyun.Models;
using Xunit.Abstractions;

namespace Upyun.Test;

public sealed class UpyunClientTests
{
    private const string Endpoint = "https://v0.api.upyun.com";
    // 测试结束会递归删除该目录及其所有内容，请不要改为根目录。
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
        var client = CreateClient();
        var testId = Guid.NewGuid().ToString("N");
        var directoryPath = $"{TestRoot}/{testId}";
        var sourcePath = $"{directoryPath}/source.txt";
        var copiedPath = $"{directoryPath}/copied.txt";
        var movedPath = $"{directoryPath}/moved.txt";
        var missingPath = $"{directoryPath}/missing.txt";
        var content = Encoding.UTF8.GetBytes($"hello upyun sdk {testId}");

        try
        {
            _output.WriteLine($"正在创建测试根目录：{TestRoot}...");
            await client.CreateDirectoryAsync(TestRoot);

            _output.WriteLine($"正在创建测试目录：{directoryPath}...");
            await client.CreateDirectoryAsync(directoryPath);

            _output.WriteLine($"正在获取目录信息：{directoryPath}...");
            var directoryInfo = await client.GetFileInfoAsync(directoryPath);
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
            var fileInfo = await client.GetFileInfoAsync(sourcePath);
            Assert.Equal("file", fileInfo.Type);
            Assert.Equal(content.Length, fileInfo.Size);

            _output.WriteLine($"正在下载文件到字节数组：{sourcePath}...");
            var downloaded = await client.DownloadFileAsync(sourcePath);
            Assert.Equal(content, downloaded);

            _output.WriteLine($"正在下载文件到流：{sourcePath}...");
            using var destination = new MemoryStream();
            await client.DownloadFileAsync(sourcePath, destination);
            Assert.Equal(content, destination.ToArray());
            
            // 这里需要等待几秒，否则可能会触发并发错误：{"msg":"concurrent put or delete","code":42900007}
            await Task.Delay(3000);
            
            _output.WriteLine($"正在复制文件：{sourcePath} -> {copiedPath}...");
            await client.CopyFileAsync(sourcePath, copiedPath);
            
            _output.WriteLine($"正在读取复制后的文件：{copiedPath}...");
            var copied = await client.DownloadFileAsync(copiedPath);
            Assert.Equal(content, copied);

            _output.WriteLine($"正在移动文件：{sourcePath} -> {movedPath}...");
            await client.MoveFileAsync(sourcePath, movedPath);

            _output.WriteLine($"正在读取移动后的文件：{movedPath}...");
            var moved = await client.DownloadFileAsync(movedPath);
            Assert.Equal(content, moved);

            _output.WriteLine($"正在获取目录文件列表：{directoryPath}...");
            var directoryList = await client.GetDirectoryListAsync(directoryPath, limit: 100);
            Assert.Contains(directoryList.Files, item => item.Name == "copied.txt");
            Assert.Contains(directoryList.Files, item => item.Name == "moved.txt");

            _output.WriteLine("正在获取服务使用量...");
            var usage = await client.GetUsageAsync();
            Assert.True(usage >= content.Length);

            _output.WriteLine($"正在验证下载不存在文件会抛出异常：{missingPath}...");
            var exception = await Assert.ThrowsAsync<UpyunException>(() => client.DownloadFileAsync(missingPath));
            Assert.NotNull(exception.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, exception.StatusCode);

            _output.WriteLine($"正在删除移动后的文件：{movedPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteFileAsync(movedPath);

            _output.WriteLine($"正在删除复制后的文件：{copiedPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteFileAsync(copiedPath);

            _output.WriteLine($"正在删除测试目录：{directoryPath}...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteDirectoryAsync(directoryPath);
        }
        finally
        {
            await DeleteDirectoryTreeIfExistsAsync(client, TestRoot);
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
        var value = Secrets.Value[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请在 Upyun.Test 的 User Secrets 中配置 {key}。");
        }

        return value;
    }

    private async Task DeleteDirectoryTreeIfExistsAsync(UpyunClient client, string path)
    {
        try
        {
            _output.WriteLine($"正在递归清理目录：{path}...");
            await DeleteDirectoryContentsAsync(client, path);

            await Task.Delay(TimeSpan.FromSeconds(1));
            await client.DeleteDirectoryAsync(path);
            _output.WriteLine($"目录清理完成：{path}");
        }
        catch (UpyunException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _output.WriteLine($"目录不存在，无需清理：{path}");
        }
        catch (UpyunException)
        {
            // 兜底清理不参与测试断言，避免掩盖主流程失败原因。
            _output.WriteLine($"递归清理目录失败，已忽略：{path}");
        }
    }

    private async Task DeleteDirectoryContentsAsync(UpyunClient client, string directoryPath)
    {
        var iter = (string?)null;
        var isEnd = false;

        do
        {
            var directoryList = await client.GetDirectoryListAsync(directoryPath, iter: iter, limit: 100);

            foreach (var item in directoryList.Files)
            {
                var itemPath = CombineRemotePath(directoryPath, item.Name);
                if (item is UpyunDirectory)
                {
                    await DeleteDirectoryContentsAsync(client, itemPath);

                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await client.DeleteDirectoryAsync(itemPath);
                    _output.WriteLine($"已删除子目录：{itemPath}");
                }
                else
                {
                    await client.DeleteFileAsync(itemPath);
                    _output.WriteLine($"已删除文件：{itemPath}");
                }
            }

            iter = directoryList.Iter;
            isEnd = directoryList.IsEnd || string.IsNullOrEmpty(iter);
        }
        while (!isEnd);
    }

    private static string CombineRemotePath(string directoryPath, string name)
    {
        if (string.IsNullOrEmpty(directoryPath) || directoryPath == "/")
        {
            return "/" + name.Trim('/');
        }

        return directoryPath.TrimEnd('/') + "/" + name.Trim('/');
    }
}
