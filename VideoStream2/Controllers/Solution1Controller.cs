using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace VideoStream2.Controllers
{
    public class Solution1Controller : Controller
    {
        /// <summary>
        /// Solution 1: simply returning a FilePathResult.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Index()
        {
            var relativepath = VirtualPathUtility.ToAbsolute(HomeController.harryPotter);
            var realpath = Server.MapPath(relativepath);
            if (System.IO.File.Exists(realpath) == true)
            {
                return File(realpath, "video/mp4");
            }

            return HttpNotFound();
        }
    }
}