using SteamKit2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.CustomSteamMessageHandler
{
    public static class UtilityTest
    {
		public static Task<T> ToLongRunningTask<T>(this AsyncJob<T> job) where T : CallbackMsg
		{
			if (job == null)
			{
				throw new ArgumentNullException(nameof(job));
			}

			job.Timeout = TimeSpan.FromSeconds(60);

			return job.ToTask();
		}

	}
}
