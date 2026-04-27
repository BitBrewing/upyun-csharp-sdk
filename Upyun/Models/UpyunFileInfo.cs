namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云返回的文件或目录信息。
    /// </summary>
    public sealed class UpyunFileInfo
    {
        /// <summary>
        /// 获取或设置又拍云文件类型；文档中的取值为 file 或 folder。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置文件大小，单位为字节。
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 获取或设置创建时间，格式为秒级 Unix 时间戳。
        /// </summary>
        public long CreatedAtUnixTime { get; set; }

        /// <summary>
        /// 获取或设置又拍云返回的文件 Content-MD5 值。
        /// </summary>
        public string ContentMd5 { get; set; }
    }
}
