using System;

namespace Octockup.Server.Controllers
{
    public static class Routes
    {
        public const string Version = "/api/v1";

        public static class Service
        {
            public const string Headers = Version + "/headers";
            public const string AppVersion = Version + "/version";
            public const string Metrics = Version + "/metrics";
            public const string Health = Version + "/health";
            public const string Time = Version + "/time";
        }
    }
}
