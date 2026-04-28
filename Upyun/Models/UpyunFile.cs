namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云文件条目。
    /// </summary>
    public sealed class UpyunFile : UpyunFileSystem
    {
        /// <summary>
        /// 获取或设置文件 MIME 类型。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置文件大小，单位为字节。
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// 获取或设置文件 Content-MD5 值。
        /// </summary>
        public string ContentMd5 { get; set; }
    }
}
