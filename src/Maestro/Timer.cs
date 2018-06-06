using System;
using System.Net.Http;
using System.Threading.Tasks;
using Maestro.Inject;
using Microsoft.Azure.WebJobs;

namespace Maestro
{
    public class Timer
    {
        [FunctionName("Test")]
        public static Task Test([HttpTrigger] HttpRequestMessage message, [Inject] Maestro maestro)
        {
            return maestro.CheckAllReposAsync();
        }

        //[FunctionName("Timer")]
        //public static Task Run([TimerTrigger("0 0 8 1/1 * ?")] TimerInfo info, [Inject] Maestro maestro)
        //{
        //    return maestro.CheckAllReposAsync();
        //}
    }
}
