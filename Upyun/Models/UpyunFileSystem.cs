using System;

namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云目录列表中的文件系统条目。
    /// </summary>
    public abstract class UpyunFileSystem
    {
        /// <summary>
        /// 获取或设置条目名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置最后修改时间。
        /// </summary>
        public DateTimeOffset LastModifiedTime { get; set; }
    }
}
