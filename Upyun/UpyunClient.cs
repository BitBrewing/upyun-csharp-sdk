using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Upyun.Models;

namespace Upyun
{
    /// <summary>
    /// 提供用于访问又拍云 REST API 的异步客户端。
    /// </summary>
    public sealed class UpyunClient : IDisposable
    {
        private const string DefaultEndpoint = "https://v0.api.upyun.com";
        private const string EndOfDirectoryListIter = "g2gCZAAEbmV4dGQAA2VvZg";

        private readonly string _bucket;
        private readonly string _operatorName;
        private readonly string _passwordMd5;
        private readonly Uri _endpoint;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;

        /// <summary>
        /// 使用独立的 <see cref="HttpClient"/> 初始化 <see cref="UpyunClient"/> 类的新实例。
        /// </summary>
        /// <param name="bucket">又拍云服务名称。</param>
        /// <param name="operatorName">又拍云操作员名称。</param>
        /// <param name="password">又拍云操作员密码。</param>
        /// <param name="endpoint">又拍云 REST API 接入点，默认使用推荐的智能选路地址。</param>
        /// <exception cref="ArgumentException">当 <paramref name="bucket"/> 或 <paramref name="operatorName"/> 为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 <paramref name="password"/> 为 <see langword="null"/> 时抛出。</exception>
        public UpyunClient(string bucket, string operatorName, string password, string endpoint = DefaultEndpoint)
            : this(bucket, operatorName, password, new HttpClient(), true, endpoint)
        {
        }

        /// <summary>
        /// 使用调用方提供的 <see cref="HttpClient"/> 初始化 <see cref="UpyunClient"/> 类的新实例。
        /// </summary>
        /// <param name="bucket">又拍云服务名称。</param>
        /// <param name="operatorName">又拍云操作员名称。</param>
        /// <param name="password">又拍云操作员密码。</param>
        /// <param name="httpClient">用于发送请求的 HTTP 客户端。</param>
        /// <param name="endpoint">又拍云 REST API 接入点，默认使用推荐的智能选路地址。</param>
        /// <exception cref="ArgumentException">当 <paramref name="bucket"/> 或 <paramref name="operatorName"/> 为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 <paramref name="password"/> 或 <paramref name="httpClient"/> 为 <see langword="null"/> 时抛出。</exception>
        public UpyunClient(
            string bucket,
            string operatorName,
            string password,
            HttpClient httpClient,
            string endpoint = DefaultEndpoint)
            : this(bucket, operatorName, password, httpClient, false, endpoint)
        {
        }

        private UpyunClient(
            string bucket,
            string operatorName,
            string password,
            HttpClient httpClient,
            bool disposeHttpClient,
            string endpoint)
        {
            if (string.IsNullOrWhiteSpace(bucket))
            {
                throw new ArgumentException("Bucket cannot be empty.", nameof(bucket));
            }

            if (string.IsNullOrWhiteSpace(operatorName))
            {
                throw new ArgumentException("Operator name cannot be empty.", nameof(operatorName));
            }

            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            _bucket = bucket.Trim('/');
            _operatorName = operatorName;
            _passwordMd5 = ComputeMd5Hex(password);
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _disposeHttpClient = disposeHttpClient;
            _endpoint = new Uri((string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint).TrimEnd('/') + "/");
        }

        /// <summary>
        /// 将本地文件上传到指定的又拍云文件路径。
        /// </summary>
        /// <param name="path">服务内的目标文件路径。</param>
        /// <param name="localFilePath">要上传的本地文件路径。</param>
        /// <param name="contentType">可选的文件类型请求头。</param>
        /// <param name="contentMd5">可选的 Content-MD5 请求头值。</param>
        /// <param name="headers">额外请求头，例如文件元信息或图片预处理参数。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步上传操作的任务。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="localFilePath"/> 为空时抛出。</exception>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task UploadFileAsync(
            string path,
            string localFilePath,
            string contentType = null,
            string contentMd5 = null,
            IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                throw new ArgumentException("Local file path cannot be empty.", nameof(localFilePath));
            }

            return UploadLocalFileAsync(path, localFilePath, contentType, contentMd5, headers, cancellationToken);
        }

        /// <summary>
        /// 将字节数组上传到指定的又拍云文件路径。
        /// </summary>
        /// <param name="path">服务内的目标文件路径。</param>
        /// <param name="content">要上传的字节内容。</param>
        /// <param name="contentType">可选的文件类型请求头。</param>
        /// <param name="contentMd5">可选的 Content-MD5 请求头值。</param>
        /// <param name="headers">额外请求头，例如文件元信息或图片预处理参数。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步上传操作的任务。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="content"/> 为 <see langword="null"/> 时抛出。</exception>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task UploadFileAsync(
            string path,
            byte[] content,
            string contentType = null,
            string contentMd5 = null,
            IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return UploadFileAsync(
                path,
                new ByteArrayContent(content),
                content.Length,
                contentType,
                contentMd5,
                headers,
                cancellationToken);
        }

        /// <summary>
        /// 将流内容上传到指定的又拍云文件路径。
        /// </summary>
        /// <param name="path">服务内的目标文件路径。</param>
        /// <param name="content">要上传的内容流。</param>
        /// <param name="contentLength">要上传的字节数；当流不可定位时必须提供。</param>
        /// <param name="contentType">可选的文件类型请求头。</param>
        /// <param name="contentMd5">可选的 Content-MD5 请求头值。</param>
        /// <param name="headers">额外请求头，例如文件元信息或图片预处理参数。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步上传操作的任务。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="content"/> 为 <see langword="null"/> 时抛出。</exception>
        /// <exception cref="ArgumentException">当 <paramref name="content"/> 不可定位且未提供 <paramref name="contentLength"/> 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="contentLength"/> 为负数时抛出。</exception>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task UploadFileAsync(
            string path,
            Stream content,
            long? contentLength = null,
            string contentType = null,
            string contentMd5 = null,
            IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            long length = GetContentLength(content, contentLength);
            return UploadFileAsync(path, new StreamContent(content), length, contentType, contentMd5, headers, cancellationToken);
        }

        /// <summary>
        /// 在当前服务内复制文件。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="destinationPath">目标文件路径。</param>
        /// <param name="metadataDirective">可选的元信息处理方式；又拍云默认复制元信息。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步复制操作的任务。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task CopyFileAsync(
            string sourcePath,
            string destinationPath,
            string metadataDirective = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IDictionary<string, string> headers = new Dictionary<string, string>
            {
                { "X-Upyun-Copy-Source", BuildHeaderPath(sourcePath) }
            };

            if (!string.IsNullOrWhiteSpace(metadataDirective))
            {
                headers.Add("X-Upyun-Metadata-Directive", metadataDirective);
            }

            return SendEmptyContentRequestAsync(HttpMethod.Put, destinationPath, null, headers, cancellationToken);
        }

        /// <summary>
        /// 在当前服务内移动或重命名文件。
        /// </summary>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="destinationPath">目标文件路径。</param>
        /// <param name="metadataDirective">可选的元信息处理方式；又拍云默认复制元信息。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步移动操作的任务。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task MoveFileAsync(
            string sourcePath,
            string destinationPath,
            string metadataDirective = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IDictionary<string, string> headers = new Dictionary<string, string>
            {
                { "X-Upyun-Move-Source", BuildHeaderPath(sourcePath) }
            };

            if (!string.IsNullOrWhiteSpace(metadataDirective))
            {
                headers.Add("X-Upyun-Metadata-Directive", metadataDirective);
            }

            return SendEmptyContentRequestAsync(HttpMethod.Put, destinationPath, null, headers, cancellationToken);
        }

        /// <summary>
        /// 下载又拍云文件并以字节数组返回。
        /// </summary>
        /// <param name="path">要下载的文件路径。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>下载得到的文件字节内容。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                path,
                null,
                0,
                null,
                null,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 下载又拍云文件并写入指定流。
        /// </summary>
        /// <param name="path">要下载的文件路径。</param>
        /// <param name="destination">用于接收文件内容的目标流。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步下载操作的任务。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="destination"/> 为 <see langword="null"/> 时抛出。</exception>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public async Task DownloadFileAsync(
            string path,
            Stream destination,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                path,
                null,
                0,
                null,
                null,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);

                using (Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await responseStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 删除又拍云文件。
        /// </summary>
        /// <param name="path">要删除的文件路径。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步删除操作的任务。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendNoBodyRequestAsync(HttpMethod.Delete, path, null, null, cancellationToken);
        }

        /// <summary>
        /// 在又拍云创建目录。
        /// </summary>
        /// <param name="path">要创建的目录路径。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步创建目录操作的任务。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendEmptyContentRequestAsync(
                HttpMethod.Post,
                path,
                null,
                new Dictionary<string, string> { { "folder", "true" } },
                cancellationToken);
        }

        /// <summary>
        /// 获取文件或目录信息。
        /// </summary>
        /// <param name="path">要查询的文件或目录路径。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>又拍云响应头中返回的文件或目录信息。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应时抛出。</exception>
        public async Task<UpyunFileInfo> GetFileInfoAsync(
            string path,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Head,
                path,
                null,
                0,
                null,
                null,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);

                return new UpyunFileInfo
                {
                    Type = GetHeaderValue(response, "x-upyun-file-type"),
                    Size = ParseLongHeader(response, "x-upyun-file-size"),
                    CreatedAtUnixTime = ParseLongHeader(response, "x-upyun-file-date"),
                    ContentMd5 = GetHeaderValue(response, "Content-Md5")
                };
            }
        }

        /// <summary>
        /// 分页获取目录文件列表。
        /// </summary>
        /// <param name="path">要列举的目录路径。</param>
        /// <param name="iter">可选的分页迭代值，来自上一次响应。</param>
        /// <param name="limit">可选的分页大小；又拍云支持 1 到 10000。</param>
        /// <param name="order">按文件名排列的顺序。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>又拍云返回的目录分页结果。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="limit"/> 超出支持范围时抛出。</exception>
        /// <exception cref="UpyunException">当又拍云返回非成功响应，或 JSON 响应无法解析时抛出。</exception>
        public async Task<UpyunDirectoryList> GetDirectoryListAsync(
            string path,
            string iter = null,
            int? limit = null,
            UpyunListOrder order = UpyunListOrder.Asc,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IDictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Accept", "application/json" }
            };

            if (!string.IsNullOrEmpty(iter))
            {
                headers.Add("x-list-iter", iter);
            }

            if (limit.HasValue)
            {
                if (limit.Value <= 0 || limit.Value > 10000)
                {
                    throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 10000.");
                }

                headers.Add("x-list-limit", limit.Value.ToString(CultureInfo.InvariantCulture));
            }

            headers.Add("x-list-order", order == UpyunListOrder.Desc ? "desc" : "asc");

            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                path,
                null,
                0,
                null,
                headers,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                UpyunDirectoryList directoryList = ParseDirectoryList(json);
                string responseIter = GetHeaderValue(response, "x-upyun-list-iter");
                if (!string.IsNullOrEmpty(responseIter))
                {
                    directoryList.Iter = responseIter;
                }

                directoryList.IsEnd = string.Equals(directoryList.Iter, EndOfDirectoryListIter, StringComparison.Ordinal);
                return directoryList;
            }
        }

        /// <summary>
        /// 获取当前服务的存储使用量，单位为字节。
        /// </summary>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>当前服务的存储使用量，单位为字节。</returns>
        /// <exception cref="UpyunException">当又拍云返回非成功响应，或使用量响应内容无法解析时抛出。</exception>
        public async Task<long> GetUsageAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                string.Empty,
                "usage",
                0,
                null,
                null,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!long.TryParse(content.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long usage))
                {
                    throw new UpyunException("Failed to parse usage response.", response.StatusCode, content);
                }

                return usage;
            }
        }

        /// <summary>
        /// 释放由当前实例创建并持有的 <see cref="HttpClient"/>。
        /// </summary>
        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private async Task UploadLocalFileAsync(
            string path,
            string localFilePath,
            string contentType,
            string contentMd5,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using (FileStream stream = File.OpenRead(localFilePath))
            {
                await UploadFileAsync(path, stream, stream.Length, contentType, contentMd5, headers, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task UploadFileAsync(
            string path,
            HttpContent content,
            long contentLength,
            string contentType,
            string contentMd5,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using (content)
            using (HttpResponseMessage response = await SendAsync(
                HttpMethod.Put,
                path,
                null,
                contentLength,
                content,
                headers,
                cancellationToken,
                contentType,
                contentMd5).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
            }
        }

        private async Task SendEmptyContentRequestAsync(
            HttpMethod method,
            string path,
            string query,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await SendAsync(
                method,
                path,
                query,
                0,
                new ByteArrayContent(new byte[0]),
                headers,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
            }
        }

        private async Task SendNoBodyRequestAsync(
            HttpMethod method,
            string path,
            string query,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await SendAsync(
                method,
                path,
                query,
                0,
                null,
                headers,
                cancellationToken).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
            }
        }

        private Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            string query,
            long contentLength,
            HttpContent content,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken,
            string contentType = null,
            string contentMd5 = null)
        {
            string requestPath = BuildRequestPath(path);
            Uri requestUri = BuildUri(requestPath, query);
            string date = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);

            HttpRequestMessage request = new HttpRequestMessage(method, requestUri);
            request.Headers.TryAddWithoutValidation("Date", date);
            request.Headers.TryAddWithoutValidation("Authorization", BuildAuthorization(method.Method, requestPath, date, contentLength));

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    AddHeader(request, header.Key, header.Value);
                }
            }

            if (content != null)
            {
                content.Headers.ContentLength = contentLength;

                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                }

                if (!string.IsNullOrWhiteSpace(contentMd5))
                {
                    content.Headers.TryAddWithoutValidation("Content-MD5", contentMd5);
                }

                request.Content = content;
            }
            else if (!string.IsNullOrWhiteSpace(contentMd5))
            {
                request.Headers.TryAddWithoutValidation("Content-MD5", contentMd5);
            }

            return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        private static void AddHeader(HttpRequestMessage request, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Header name cannot be empty.", nameof(name));
            }

            if (request.Content != null && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                request.Content.Headers.TryAddWithoutValidation(name, value);
                return;
            }

            request.Headers.TryAddWithoutValidation(name, value);
        }

        private string BuildAuthorization(string method, string requestPath, string date, long contentLength)
        {
            string signatureSource = string.Join(
                "&",
                method.ToUpperInvariant(),
                requestPath,
                date,
                contentLength.ToString(CultureInfo.InvariantCulture),
                _passwordMd5);
            string signature = ComputeMd5Hex(signatureSource);

            return string.Concat("UpYun ", _operatorName, ":", signature);
        }

        private string BuildRequestPath(string path)
        {
            string normalizedPath = NormalizeObjectPath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return "/" + EscapePathSegment(_bucket) + "/";
            }

            return "/" + EscapePathSegment(_bucket) + "/" + EncodePath(normalizedPath);
        }

        private Uri BuildUri(string requestPath, string query)
        {
            string relative = requestPath.TrimStart('/');
            if (!string.IsNullOrEmpty(query))
            {
                relative += "?" + query;
            }

            return new Uri(_endpoint, relative);
        }

        private string BuildHeaderPath(string path)
        {
            string normalizedPath = NormalizeObjectPath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                throw new ArgumentException("Source path cannot be empty.", nameof(path));
            }

            return "/" + _bucket + "/" + normalizedPath;
        }

        private static string NormalizeObjectPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.Trim('/');
        }

        private static string EncodePath(string path)
        {
            return string.Join("/", path.Split('/').Select(EscapePathSegment).ToArray());
        }

        private static string EscapePathSegment(string segment)
        {
            return Uri.EscapeDataString(segment);
        }

        private static long GetContentLength(Stream content, long? contentLength)
        {
            if (contentLength.HasValue)
            {
                if (contentLength.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(contentLength), "Content length cannot be negative.");
                }

                return contentLength.Value;
            }

            if (!content.CanSeek)
            {
                throw new ArgumentException("Content length is required when the stream cannot seek.", nameof(content));
            }

            return content.Length - content.Position;
        }

        private static UpyunDirectoryList ParseDirectoryList(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    UpyunDirectoryList result = new UpyunDirectoryList();

                    if (root.TryGetProperty("iter", out JsonElement iterElement) && iterElement.ValueKind == JsonValueKind.String)
                    {
                        result.Iter = iterElement.GetString();
                    }

                    if (root.TryGetProperty("files", out JsonElement filesElement) && filesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in filesElement.EnumerateArray())
                        {
                            result.Files.Add(new UpyunDirectoryItem
                            {
                                Name = GetJsonString(item, "name"),
                                Type = GetJsonString(item, "type"),
                                Length = GetJsonInt64(item, "length"),
                                LastModifiedUnixTime = GetJsonInt64(item, "last_modified")
                            });
                        }
                    }

                    return result;
                }
            }
            catch (JsonException exception)
            {
                throw new UpyunException("Failed to parse directory list response.", exception);
            }
        }

        private static string GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
        }

        private static long GetJsonInt64(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return 0;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return 0;
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string content = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string message = string.IsNullOrWhiteSpace(content)
                ? "Upyun request failed with status code " + (int)response.StatusCode + "."
                : content;

            throw new UpyunException(message, response.StatusCode, content);
        }

        private static string GetHeaderValue(HttpResponseMessage response, string headerName)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues(headerName, out values))
            {
                return values.FirstOrDefault();
            }

            if (response.Content != null && response.Content.Headers.TryGetValues(headerName, out values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        private static long ParseLongHeader(HttpResponseMessage response, string headerName)
        {
            string value = GetHeaderValue(response, headerName);
            long result;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static string ComputeMd5Hex(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            using (MD5 md5 = MD5.Create())
            {
                return ToHex(md5.ComputeHash(bytes));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
