namespace Upyun.Models
{
    /// <summary>
    /// 表示目录列表中的一个文件或目录项。
    /// </summary>
    public sealed class UpyunDirectoryItem
    {
        /// <summary>
        /// 获取或设置条目名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置又拍云返回的条目类型。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置条目大小，单位为字节。
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// 获取或设置最后修改时间，格式为秒级 Unix 时间戳。
        /// </summary>
        public long LastModifiedUnixTime { get; set; }
    }
}
