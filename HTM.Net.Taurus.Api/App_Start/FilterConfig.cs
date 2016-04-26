using System.Web;
using System.Web.Mvc;

namespace HTM.Net.Taurus.Api
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
