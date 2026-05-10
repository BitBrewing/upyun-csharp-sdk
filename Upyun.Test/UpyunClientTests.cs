using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
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
        var client = CreateClient();
        var testId = $"upyun-sdk-test-{Guid.NewGuid():N}";
        var directoryPath = CombineRemotePath(TestRoot, testId);
        var sourcePath = $"{directoryPath}/source.png";
        var copiedPath = $"{directoryPath}/copied.png";
        var movedPath = $"{directoryPath}/moved.png";
        var missingPath = $"{directoryPath}/missing.txt";
        var content = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        try
        {
            _output.WriteLine($"正在创建测试目录：{directoryPath}...");
            await client.CreateDirectoryAsync(directoryPath);

            _output.WriteLine($"正在获取目录信息：{directoryPath}...");
            var directoryInfo = await client.GetFileInfoAsync(directoryPath);
            Assert.IsType<UpyunDirectory>(directoryInfo);

            _output.WriteLine($"正在上传文件：{sourcePath}...");
            await client.UploadFileAsync(
                sourcePath,
                content,
                metadata: new Dictionary<string, string>
                {
                    { "sdk-test", testId }
                });

            _output.WriteLine($"正在获取文件信息：{sourcePath}...");
            var fileInfo = await client.GetFileInfoAsync(sourcePath);
            var sourceFileInfo = Assert.IsType<UpyunFile>(fileInfo);
            Assert.Equal("image/png", sourceFileInfo.Type);
            Assert.Equal(content.Length, sourceFileInfo.Length);

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
            Assert.Contains(directoryList.Files, item => item.Name == "copied.png");
            Assert.Contains(directoryList.Files, item => item.Name == "moved.png");

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

        }
        finally
        {
            await DeleteDirectoryTreeIfExistsAsync(client, directoryPath);
        }
    }

    [Fact]
    public async Task FormApi_UploadImage_WithGeneratedAuthorization_WorksInRealEnvironment()
    {
        _output.WriteLine("正在读取测试配置...");
        var client = CreateClient();
        var testId = $"upyun-form-test-{Guid.NewGuid():N}";
        var directoryPath = CombineRemotePath(TestRoot, testId);
        var content = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var fileMd5 = ComputeMd5Hex(content);
        var remotePath = CombineRemotePath(directoryPath, $"{fileMd5.Substring(0, 1)}/{fileMd5}.png");
        var uploadAuthorization = client.GenerateFormUploadAuthorization(remotePath, TimeSpan.FromHours(1));

        try
        {
            using var httpClient = new HttpClient();
            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "file", "source.png");
            form.Add(new StringContent(uploadAuthorization.Policy), "policy");
            form.Add(new StringContent(uploadAuthorization.Authorization), "authorization");

            _output.WriteLine($"正在通过 Form API 上传图片：{remotePath}...");
            using var response = await httpClient.PostAsync(uploadAuthorization.UploadUrl, form);
            var responseContent = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            Assert.Equal(200, root.GetProperty("code").GetInt32());
            Assert.Equal(remotePath, root.GetProperty("url").GetString());
            Assert.Equal(content.Length, root.GetProperty("file_size").GetInt64());
            Assert.Equal("image/png", root.GetProperty("mimetype").GetString());

            _output.WriteLine($"正在下载 Form API 上传后的图片：{remotePath}...");
            var downloaded = await client.DownloadFileAsync(remotePath);
            Assert.Equal(content, downloaded);
        }
        finally
        {
            await DeleteDirectoryTreeIfExistsAsync(client, directoryPath);
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

    private static string ComputeMd5Hex(byte[] content)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(content)).ToLowerInvariant();
    }

    private async Task DeleteDirectoryTreeIfExistsAsync(UpyunClient client, string path)
    {
        try
        {
            _output.WriteLine($"正在递归清理目录：{path}...");

            await client.EnumerateDirectoryAsync(
                path,
                async (itemPath, item) =>
                {
                    if (item is UpyunDirectory)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        await client.DeleteDirectoryAsync(itemPath);
                        _output.WriteLine($"已删除子目录：{itemPath}");
                    }
                    else
                    {
                        await client.DeleteFileAsync(itemPath);
                        _output.WriteLine($"已删除文件：{itemPath}");
                    }
                },
                limit: 100);

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

    private static string CombineRemotePath(string directoryPath, string name)
    {
        if (string.IsNullOrEmpty(directoryPath) || directoryPath == "/")
        {
            return "/" + name.Trim('/');
        }

        return directoryPath.TrimEnd('/') + "/" + name.Trim('/');
    }
}
