using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Grpc.Core;
using MagicOnion.Server;
using MagicOnion.Server.Diagnostics;
using MagicOnion.Server.Hubs;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace NineChronicles.Headless;

/// <summary>
/// Collect OpemTelemetry Meter Metrics.
/// </summary>
public class OpenTelemetryCollectorLogger : IMagicOnionLogger
{
    static readonly string MethodKey = "method";

    readonly IEnumerable<KeyValuePair<string, object?>> defaultLabels;
    readonly ConcurrentDictionary<string, HashSet<KeyValuePair<string, object?>>> labelCache = new ConcurrentDictionary<string, HashSet<KeyValuePair<string, object?>>>();
    readonly ConcurrentDictionary<string, HashSet<KeyValuePair<string, object?>>> broadcastLabelCache = new ConcurrentDictionary<string, HashSet<KeyValuePair<string, object?>>>();

    readonly Histogram<double> buildServiceDefinitionMeasure;
    readonly Counter<long> unaryRequestCounter;
    readonly Histogram<long> unaryRequestSizeMeasure;
    readonly Histogram<long> unaryResponseSizeMeasure;
    readonly Counter<long> unaryErrorCounter;
    readonly Histogram<double> unaryElapsedMeasure;

    readonly Counter<long> streamingHubErrorCounter;
    readonly Histogram<double> streamingHubElapsedMeasure;
    readonly Counter<long> streamingHubRequestCounter;
    readonly Histogram<long> streamingHubRequestSizeMeasure;
    readonly Histogram<long> streamingHubResponseSizeMeasure;
    readonly Counter<long> streamingHubConnectCounter;
    readonly Counter<long> streamingHubDisconnectCounter;

    readonly Counter<long> broadcastRequestCounter;
    readonly Histogram<long> broadcastRequestSizeMeasure;
    readonly Counter<long> broadcastGroupCounter;

    public OpenTelemetryCollectorLogger(MeterProvider meterProvider, string metricsPrefix = "magiconion", string? version = null, IEnumerable<KeyValuePair<string, object?>>? defaultLabels = null)
    {
        if (meterProvider == null)
        {
            throw new ArgumentNullException(nameof(meterProvider));
        }

        // configure defaultTags included as default tag
        this.defaultLabels = defaultLabels ?? Array.Empty<KeyValuePair<string, object?>>();

        Meter meter = new("MagicOnion", version);

        // Service build time. ms
        buildServiceDefinitionMeasure = meter.CreateHistogram<double>($"{metricsPrefix}_buildservicedefinition_duration_milliseconds"); // sum

        // Unary request count. num
        unaryRequestCounter = meter.CreateCounter<long>($"{metricsPrefix}_unary_requests_count"); // sum
        // Unary API request size. bytes
        unaryRequestSizeMeasure = meter.CreateHistogram<long>($"{metricsPrefix}_unary_request_size"); // sum
        // Unary API response size. bytes
        unaryResponseSizeMeasure = meter.CreateHistogram<long>($"{metricsPrefix}_unary_response_size"); // sum
        // Unary API error Count. num
        unaryErrorCounter = meter.CreateCounter<long>($"{metricsPrefix}_unary_error_count"); // sum
        // Unary API elapsed time. ms
        unaryElapsedMeasure = meter.CreateHistogram<double>($"{metricsPrefix}_unary_elapsed_milliseconds"); // sum

        // StreamingHub error Count. num
        streamingHubErrorCounter = meter.CreateCounter<long>($"{metricsPrefix}_streaminghub_error_count"); // sum
        // StreamingHub elapsed time. ms
        streamingHubElapsedMeasure = meter.CreateHistogram<double>($"{metricsPrefix}_streaminghub_elapsed_milliseconds"); // sum
        // StreamingHub request count. num
        streamingHubRequestCounter = meter.CreateCounter<long>($"{metricsPrefix}_streaminghub_requests_count"); // sum
        // StreamingHub request size. bytes
        streamingHubRequestSizeMeasure = meter.CreateHistogram<long>($"{metricsPrefix}_streaminghub_request_size"); // sum
        // StreamingHub response size. bytes
        streamingHubResponseSizeMeasure = meter.CreateHistogram<long>($"{metricsPrefix}_streaminghub_response_size"); // sum
        // ConnectCount - DisconnectCount = current connect count. (successfully disconnected)
        // StreamingHub connect count. num
        streamingHubConnectCounter = meter.CreateCounter<long>($"{metricsPrefix}_streaminghub_connect_count"); // sum
        // StreamingHub disconnect count. num
        streamingHubDisconnectCounter = meter.CreateCounter<long>($"{metricsPrefix}_streaminghub_disconnect_count"); // sum

        // HubBroadcast request count. num
        broadcastRequestCounter = meter.CreateCounter<long>($"{metricsPrefix}_broadcast_requests_count"); // sum
        // HubBroadcast request size. num
        broadcastRequestSizeMeasure = meter.CreateHistogram<long>($"{metricsPrefix}_broadcast_request_size"); // sum
        // HubBroadcast group count. num
        broadcastGroupCounter = meter.CreateCounter<long>($"{metricsPrefix}_broadcast_group_count"); // sum
    }

    IEnumerable<KeyValuePair<string, object?>> CreateLabel(ServiceContext context)
    {
        // Unary start from /{UnaryInterface}/{Method}
        var value = context.CallContext.Method;
        var label = labelCache.GetOrAdd(value, new HashSet<KeyValuePair<string, object?>>(defaultLabels)
        {
            new KeyValuePair<string, object?>( MethodKey, context.CallContext.Method),
        });
        return label;
    }
    IEnumerable<KeyValuePair<string, object?>> CreateLabel(StreamingHubContext context)
    {
        // StreamingHub start from {HubInterface}/{Method}
        var value = "/" + context.Path;
        var label = labelCache.GetOrAdd(value, new HashSet<KeyValuePair<string, object?>>(defaultLabels)
        {
            new KeyValuePair<string, object?>( MethodKey, value),
        });
        return label;
    }
    IEnumerable<KeyValuePair<string, object?>> CreateLabel(string value)
    {
        var label = labelCache.GetOrAdd(value, new HashSet<KeyValuePair<string, object?>>(defaultLabels)
        {
            new KeyValuePair<string, object?>( MethodKey, value),
        });
        return label;
    }
    IEnumerable<KeyValuePair<string, object?>> CreateBroadcastLabel(string value)
    {
        var label = broadcastLabelCache.GetOrAdd(value, new HashSet<KeyValuePair<string, object?>>(defaultLabels)
        {
            new KeyValuePair<string, object?>( "GroupName", value),
        });
        return label;
    }

    public void BeginBuildServiceDefinition()
    {
    }

    public void EndBuildServiceDefinition(double elapsed)
    {
        buildServiceDefinitionMeasure.Record(elapsed, tags: CreateLabel(nameof(EndBuildServiceDefinition)).ToArray());
    }

    public void BeginInvokeMethod(ServiceContext context, Type type)
    {
    }

    public void BeginInvokeMethod(ServiceContext context, byte[] request, Type type)
    {   
        var label = CreateLabel(context).ToArray();
        if (context.MethodType == MethodType.DuplexStreaming && context.CallContext.Method.EndsWith("/Connect"))
        {
            streamingHubConnectCounter.Add(1, label);
        }
        else if (context.MethodType == MethodType.Unary)
        {
            unaryRequestCounter.Add(1, label);
            unaryRequestSizeMeasure.Record(request.Length, label);
        }
    }

    public void EndInvokeMethod(ServiceContext context, Type type, double elapsed, bool isErrorOrInterrupted)
    {
    }

    public void EndInvokeMethod(ServiceContext context, byte[] response, Type type, double elapsed, bool isErrorOrInterrupted)
    {
        var label = CreateLabel(context).ToArray();
        if (context.MethodType == MethodType.DuplexStreaming && context.CallContext.Method.EndsWith("/Connect"))
        {
            streamingHubDisconnectCounter.Add(1, label);
        }
        else if (context.MethodType == MethodType.Unary)
        {
            unaryElapsedMeasure.Record(elapsed, label);
            unaryResponseSizeMeasure.Record(response.LongLength, label);
            if (isErrorOrInterrupted)
            {
                unaryErrorCounter.Add(1, label);
            }
        }
    }

    public void BeginInvokeHubMethod(StreamingHubContext context, ReadOnlyMemory<byte> request, Type type)
    {
        
        var label = CreateLabel(context).ToArray();
        streamingHubRequestCounter.Add(1, label);
        streamingHubRequestSizeMeasure.Record(request.Length, label);
    }

    public void EndInvokeHubMethod(StreamingHubContext context, int responseSize, Type? type, double elapsed, bool isErrorOrInterrupted)
    {
        
        var label = CreateLabel(context).ToArray();
        streamingHubElapsedMeasure.Record(elapsed, label);
        streamingHubResponseSizeMeasure.Record(responseSize, label);
        if (isErrorOrInterrupted)
        {
            streamingHubErrorCounter.Add(1, label);
        }
    }

    public void InvokeHubBroadcast(string groupName, int responseSize, int broadcastGroupCount)
    {
        
        broadcastRequestCounter.Add(1, CreateBroadcastLabel(groupName).ToArray());
        broadcastGroupCounter.Add(broadcastGroupCount, CreateBroadcastLabel(groupName).ToArray());
        broadcastRequestSizeMeasure.Record(responseSize, CreateBroadcastLabel(groupName).ToArray());
    }

    public void ReadFromStream(ServiceContext context, Type type, bool complete)
    {
    }

    public void ReadFromStream(ServiceContext context, byte[] readData, Type type, bool complete)
    {
    }

    public void WriteToStream(ServiceContext context, Type type)
    {
    }

    public void WriteToStream(ServiceContext context, byte[] writeData, Type type)
    {
    }

    public void Error(Exception ex, ServerCallContext context)
    {
    }

    public void Error(Exception ex, StreamingHubContext context)
    {
    }
}
