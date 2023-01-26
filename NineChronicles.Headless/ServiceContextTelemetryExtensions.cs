using System.Diagnostics;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;

namespace NineChronicles.Headless;

public static class ServiceContextTelemetryExtensions
{
    internal static void SetTraceContext(this ServiceContext context, ActivityContext activityContext)
    {
        context.Items[MagicOnionTelemetry.ServiceContextItemKeyTrace] = activityContext;
    }

    public static ActivityContext GetTraceContext(this ServiceContext context)
    {
        return (ActivityContext)context.Items[MagicOnionTelemetry.ServiceContextItemKeyTrace];
    }
}

public static class StreamingHubContextTelemetryExtensions
{
    internal static void SetTraceContext(this StreamingHubContext context, ActivityContext activityContext)
    {
        context.Items[MagicOnionTelemetry.ServiceContextItemKeyTrace] = activityContext;
    }

    public static ActivityContext GetTraceContext(this StreamingHubContext context)
    {
        return (ActivityContext)context.Items[MagicOnionTelemetry.ServiceContextItemKeyTrace];
    }
}
