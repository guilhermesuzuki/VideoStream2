using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace VideoStream2.Controllers
{
    public class Solution2Controller : Controller
    {
        /// <summary>
        /// Solution 2: simply returning a FileStreamResult.
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            var relativepath = VirtualPathUtility.ToAbsolute(HomeController.harryPotter);
            var realpath = Server.MapPath(relativepath);
            if (System.IO.File.Exists(realpath) == true)
            {
                var bytes = System.IO.File.ReadAllBytes(realpath);
                var stream = new MemoryStream(bytes);
                return File(stream, "video/mp4");
            }

            return HttpNotFound();
        }
    }
}