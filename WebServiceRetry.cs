using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace Vtiger
{
    public static class WebServiceRetry
    {
        public static void ExponentialBackoff(Action action, TimeSpan slotTime, int retryCount)
        {
            WebException caught = null;
            Random random = new Random();
            for (int retry = 1; retry <= retryCount; ++retry)
            {
                try
                {
                    action();
                    return;
                }
                catch (WebException e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    System.Diagnostics.Debug.WriteLine(String.Format("Web service retry {0} failed", retry));
                    caught = e;
                    Thread.Sleep(random.Next(retry * retry) * (int)slotTime.TotalMilliseconds);
                }
            }
            throw caught;
        }
    }
}
