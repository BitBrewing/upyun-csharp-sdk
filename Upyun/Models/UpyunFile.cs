namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云目录列表中的文件条目。
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
    }
}
