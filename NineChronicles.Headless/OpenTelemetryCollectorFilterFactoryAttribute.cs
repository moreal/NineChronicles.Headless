using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MagicOnion.Server;
using MagicOnion.Server.Filters;
using MagicOnion.Server.Hubs;
using MagicOnion.Server.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;

namespace NineChronicles.Headless;

/// <summary>
/// Collect OpenTelemetry Tracing for Global filter. Handle Unary and most outside logging.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public class OpenTelemetryCollectorFilterFactoryAttribute : Attribute, IMagicOnionFilterFactory<MagicOnionFilterAttribute>
{
    public int Order { get; set; }

    MagicOnionFilterAttribute IMagicOnionFilterFactory<MagicOnionFilterAttribute>.CreateInstance(IServiceProvider serviceProvider)
    {
        return new OpenTelemetryCollectorFilterAttribute(serviceProvider.GetRequiredService<ActivitySource>(), serviceProvider.GetRequiredService<MagicOnionOpenTelemetryOptions>());
    }
}

internal class OpenTelemetryCollectorFilterAttribute : MagicOnionFilterAttribute
{
    readonly ActivitySource source;
    readonly MagicOnionOpenTelemetryOptions telemetryOption;

    public OpenTelemetryCollectorFilterAttribute(ActivitySource activitySource, MagicOnionOpenTelemetryOptions telemetryOption)
    {
        this.source = activitySource;
        this.telemetryOption = telemetryOption;
    }

    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        await next(context);
    }
}

/// <summary>
/// Collect OpenTelemetry Tracing for StreamingHub Filter. Handle Streaming Hub logging.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public class OpenTelemetryHubCollectorFilterFactoryAttribute : Attribute, IMagicOnionFilterFactory<StreamingHubFilterAttribute>
{
    public int Order { get; set; }

    StreamingHubFilterAttribute IMagicOnionFilterFactory<StreamingHubFilterAttribute>.CreateInstance(IServiceProvider serviceProvider)
    {
        return new OpenTelemetryHubCollectorFilterAttribute(serviceProvider.GetRequiredService<ActivitySource>(), serviceProvider.GetRequiredService<MagicOnionOpenTelemetryOptions>());
    }
}

internal class OpenTelemetryHubCollectorFilterAttribute : StreamingHubFilterAttribute
{
    readonly ActivitySource source;
    readonly MagicOnionOpenTelemetryOptions telemetryOption;

    public OpenTelemetryHubCollectorFilterAttribute(ActivitySource activitySource, MagicOnionOpenTelemetryOptions telemetryOption)
    {
        this.source = activitySource;
        this.telemetryOption = telemetryOption;
    }

    public override async ValueTask Invoke(StreamingHubContext context, Func<StreamingHubContext, ValueTask> next)
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/rpc.md#grpc
        using var activity =
            source.StartActivity($"{context.ServiceContext.MethodType}:/{context.Path}", ActivityKind.Server) ??
            throw new InvalidOperationException("Failed to start activity.");

        // add trace context to service context. it allows user to add their span directly to this hub
        context.SetTraceContext(activity.Context);

        try
        {
            activity.SetTag("grpc.method", context.ServiceContext.MethodType.ToString());
            activity.SetTag("rpc.system", "grpc");
            activity.SetTag("rpc.service", context.ServiceContext.ServiceType.Name);
            activity.SetTag("rpc.method", $"/{context.Path}");
            activity.SetTag("net.peer.ip", context.ServiceContext.CallContext.Peer);
            activity.SetTag("net.host.name", context.ServiceContext.CallContext.Host);
            activity.SetTag("message.type", "RECIEVED");
            activity.SetTag("message.id", context.ServiceContext.ContextId.ToString());
            activity.SetTag("message.uncompressed_size", context.Request.Length.ToString());

            activity.SetTag("magiconion.peer.ip", context.ServiceContext.CallContext.Peer);
            activity.SetTag("magiconion.auth.enabled", (!string.IsNullOrEmpty(context.ServiceContext.CallContext.AuthContext.PeerIdentityPropertyName)).ToString());
            activity.SetTag("magiconion.auth.peer.authenticated", context.ServiceContext.CallContext.AuthContext.IsPeerAuthenticated.ToString());

            await next(context);

            activity.SetTag("grpc.status_code", ((long)context.ServiceContext.CallContext.Status.StatusCode).ToString());
            activity.SetStatus(OpenTelemetrygRpcStatusHelper.ConvertStatus(context.ServiceContext.CallContext.Status.StatusCode));
        }
        catch (Exception ex)
        {
            activity.SetTag("exception", ex.ToString());
            activity.SetTag("grpc.status_code", ((long)context.ServiceContext.CallContext.Status.StatusCode).ToString());
            activity.SetTag("grpc.status_detail", context.ServiceContext.CallContext.Status.Detail);
            activity.SetStatus(OpenTelemetrygRpcStatusHelper.ConvertStatus(context.ServiceContext.CallContext.Status.StatusCode));
            throw;
        }
    }
}
