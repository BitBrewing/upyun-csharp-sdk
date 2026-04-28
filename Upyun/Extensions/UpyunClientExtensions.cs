using System;
using System.Threading;
using System.Threading.Tasks;
using Upyun.Models;

namespace Upyun
{
    /// <summary>
    /// 提供 <see cref="UpyunClient"/> 的扩展方法。
    /// </summary>
    public static class UpyunClientExtensions
    {
        /// <summary>
        /// 递归枚举指定目录下的所有文件和目录，并对每个条目执行回调。
        /// </summary>
        /// <param name="client">又拍云客户端。</param>
        /// <param name="path">要枚举的目录路径。</param>
        /// <param name="callback">枚举到文件或目录时执行的回调，参数为完整路径和条目信息；目录会在其子项之后回调。</param>
        /// <param name="limit">可选的分页大小；又拍云支持 1 到 10000。</param>
        /// <param name="order">按文件名排列的顺序。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步枚举操作的任务。</returns>
        public static async Task EnumerateDirectoryAsync(
            this UpyunClient client,
            string path,
            Func<string, UpyunFileSystem, Task> callback,
            int? limit = null,
            UpyunListOrder order = UpyunListOrder.Asc,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            string iter = null;
            bool isEnd;

            do
            {
                UpyunDirectoryList directoryList = await client.GetDirectoryListAsync(
                    path,
                    iter,
                    limit,
                    order,
                    cancellationToken).ConfigureAwait(false);

                foreach (UpyunFileSystem item in directoryList.Files)
                {
                    string itemPath = CombineRemotePath(path, item.Name);

                    if (item is UpyunDirectory)
                    {
                        await client.EnumerateDirectoryAsync(
                            itemPath,
                            callback,
                            limit,
                            order,
                            cancellationToken).ConfigureAwait(false);
                    }

                    await callback(itemPath, item).ConfigureAwait(false);
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
}