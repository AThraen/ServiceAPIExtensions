using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace ServiceAPIExtensions.Business
{
    public class BinaryMediaTypeFormatter : MediaTypeFormatter
    {

        private static Type _supportedType = typeof(byte[]);
        private const int BufferSize = 8192; // 8K 

        public BinaryMediaTypeFormatter()
        {
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
        }

        public override bool CanReadType(Type type)
        {
            return type == _supportedType;
        }

        public override bool CanWriteType(Type type)
        {
            return type == _supportedType;
        }

        public override Task<object> ReadFromStreamAsync(Type type, Stream stream,
         HttpContent content, IFormatterLogger formatterLogger)
        {
            var taskSource = new TaskCompletionSource<object>();
            try
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms, BufferSize);
                taskSource.SetResult(ms.ToArray());
            }
            catch (Exception e)
            {
                taskSource.SetException(e);
            }
            return taskSource.Task;
        }

        public override Task WriteToStreamAsync(Type type, object value, Stream stream,
         HttpContent content, TransportContext transportContext)
        {
            var taskSource = new TaskCompletionSource<object>();
            try
            {
                if (value == null)
                    value = new byte[0];
                var ms = new MemoryStream((byte[])value);
                ms.CopyTo(stream);
                taskSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskSource.SetException(e);
            }
            return taskSource.Task;
        }
    }
}