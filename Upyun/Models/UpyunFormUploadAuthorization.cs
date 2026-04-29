namespace Upyun.Models
{
    /// <summary>
    /// 表示又拍云 Form API 上传所需的授权信息。
    /// </summary>
    public sealed class UpyunFormUploadAuthorization
    {
        /// <summary>
        /// 获取或设置 Form API 上传地址。
        /// </summary>
        public string UploadUrl { get; set; }

        /// <summary>
        /// 获取或设置 Base64 编码后的上传策略。
        /// </summary>
        public string Policy { get; set; }

        /// <summary>
        /// 获取或设置上传授权签名。
        /// </summary>
        public string Authorization { get; set; }
    }
}
