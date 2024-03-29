﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace cow_merger_service
{
    /// <summary>
    ///     Formatter that allows content of type text/plain and application/octet stream
    ///     or no content type to be parsed to raw data. Allows for a single input parameter
    ///     in the form of:
    ///     public string RawString([FromBody] string data)
    ///     public byte[] RawData([FromBody] byte[] data)
    /// </summary>
    public class RawRequestBodyFormatter : InputFormatter
    {
        public RawRequestBodyFormatter()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/plain"));
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
        }


        /// <summary>
        ///     Allow text/plain, application/octet-stream and no content type to
        ///     be processed
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override bool CanRead(InputFormatterContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            string contentType = context.HttpContext.Request.ContentType;
            if (string.IsNullOrEmpty(contentType) || contentType == "text/plain" ||
                contentType == "application/octet-stream")
                return true;

            return false;
        }

        /// <summary>
        ///     Handle text/plain or no content type for string results
        ///     Handle application/octet-stream for byte[] results
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            HttpRequest request = context.HttpContext.Request;
            string contentType = context.HttpContext.Request.ContentType;


            if (string.IsNullOrEmpty(contentType) || contentType == "text/plain")
                using (StreamReader reader = new(request.Body))
                {
                    string content = await reader.ReadToEndAsync();
                    return await InputFormatterResult.SuccessAsync(content);
                }

            if (contentType == "application/octet-stream")
                using (MemoryStream ms = new MemoryStream(2048))
                {   
                    await request.Body.CopyToAsync(ms);
                    byte[] content = ms.ToArray();
                    return await InputFormatterResult.SuccessAsync(content);
                }

            return await InputFormatterResult.FailureAsync();
        }
    }
}