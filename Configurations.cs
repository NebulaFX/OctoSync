using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoSync
{
    class Configurations
    {
        // SQL Information
        public static string GeneralLogUser { get; set; }
        public static string Generic_SQL_String { get; set; } = "Data Source=161.97.74.117,16666; Initial Catalog=LogDatabase; User ID=GeneralLogUser; Password=GeneralLogUser";
        public static string DefaultConnectionString { get; set; } = "Data Source=161.97.74.117,16666; Initial Catalog=LogDatabase; User ID=@; Password=^";

        // FTP Information
        public static string FTP_Username { get; set; } = "Administrator";
        public static string FTP_Password { get; set; } = "WEETABIX70sToTp";
        public static string FTP_IP { get; set; } = "161.97.74.117";
    }
}
