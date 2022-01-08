using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace VideoStream2.Controllers
{
    public class HomeController : Controller
    {
        /// <summary>
        /// 
        /// </summary>
        public static readonly string harryPotter = "~/App_Data/files/videos/1feb049e-f508-471c-a35d-84be69f2c237/video.mp4";

        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }
    }
}
