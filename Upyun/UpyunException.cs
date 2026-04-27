using System;
using System.Net;

namespace Upyun
{
    /// <summary>
    /// 表示又拍云返回的错误，或处理又拍云响应时产生的错误。
    /// </summary>
    public class UpyunException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="UpyunException"/> 类的新实例。
        /// </summary>
        public UpyunException()
        {
        }

        /// <summary>
        /// 使用指定错误消息初始化 <see cref="UpyunException"/> 类的新实例。
        /// </summary>
        /// <param name="message">异常消息。</param>
        public UpyunException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 使用指定错误消息和内部异常初始化 <see cref="UpyunException"/> 类的新实例。
        /// </summary>
        /// <param name="message">异常消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public UpyunException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// 使用 HTTP 响应详情初始化 <see cref="UpyunException"/> 类的新实例。
        /// </summary>
        /// <param name="message">异常消息。</param>
        /// <param name="statusCode">又拍云返回的 HTTP 状态码。</param>
        /// <param name="responseContent">又拍云返回的响应内容。</param>
        public UpyunException(string message, HttpStatusCode statusCode, string responseContent)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }

        /// <summary>
        /// 获取又拍云返回的 HTTP 状态码；如果不可用则为空。
        /// </summary>
        public HttpStatusCode? StatusCode { get; private set; }

        /// <summary>
        /// 获取又拍云返回的响应内容；如果不可用则为空。
        /// </summary>
        public string ResponseContent { get; private set; }
    }
}
