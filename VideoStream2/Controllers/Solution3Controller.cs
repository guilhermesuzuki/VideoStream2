using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Mvc;

namespace VideoStream2.Controllers
{
    public class Solution3Controller : ApiController
    {
        [System.Web.Http.HttpGet]
        public HttpResponseMessage Index()
        {
            var httpResponse = Request.CreateResponse();
            httpResponse.Content = new PushStreamContent(WriteContentToStream);
            return httpResponse;
        }

        public async void WriteContentToStream(Stream outputStream, HttpContent content, TransportContext transportContext)
        {
            //path of file which we have to read//  
            var relativepath = VirtualPathUtility.ToAbsolute(HomeController.harryPotter);
            var filepath = HostingEnvironment.MapPath(relativepath);

            //here set the size of buffer, you can set any size  
            int bufferSize = 1000;
            byte[] buffer = new byte[bufferSize];
            //here we re using FileStream to read file from server//  
            using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int totalSize = (int)fileStream.Length;
                /*here we are saying read bytes from file as long as total size of file 

                is greater then 0*/
                while (totalSize > 0)
                {
                    int count = totalSize > bufferSize ? bufferSize : totalSize;
                    //here we are reading the buffer from orginal file  
                    int sizeOfReadedBuffer = fileStream.Read(buffer, 0, count);
                    //here we are writing the readed buffer to output//  
                    await outputStream.WriteAsync(buffer, 0, sizeOfReadedBuffer);
                    //and finally after writing to output stream decrementing it to total size of file.  
                    totalSize -= sizeOfReadedBuffer;
                }
            }
        }
    }
}