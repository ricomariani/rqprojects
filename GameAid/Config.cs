using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameAid
{
    class Config
    {
        // change this to your hosting site
        public static string server = "localhost";
        public static string httpserver = "http://" + server + "/";
        public static string uploads = httpserver + "uploads/";
        public static string uploads_gameaid = uploads + "gameaid/";
        public static string uploads_mapping = uploads + "Mapping/";
    }
}
