using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DupFiles
{
    public static class Config
    {
        public static string[] ExcludedFileTypes
        {
            get { return new string[] { "ini", "inf", "db", "svn-base", "css", "nfo" }; }
        }
    }
}
