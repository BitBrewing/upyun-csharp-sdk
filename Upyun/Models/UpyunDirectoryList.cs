using System.Collections.Generic;

namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云返回的目录分页列表。
    /// </summary>
    public sealed class UpyunDirectoryList
    {
        /// <summary>
        /// 初始化 <see cref="UpyunDirectoryList"/> 类的新实例。
        /// </summary>
        public UpyunDirectoryList()
        {
            Files = new List<UpyunDirectoryItem>();
        }

        /// <summary>
        /// 获取当前分页中的文件和目录。
        /// </summary>
        public IList<UpyunDirectoryItem> Files { get; private set; }

        /// <summary>
        /// 获取或设置下一次请求使用的分页迭代值。
        /// </summary>
        public string Iter { get; set; }

        /// <summary>
        /// 获取或设置当前分页是否为最后一页。
        /// </summary>
        public bool IsEnd { get; set; }
    }
}
