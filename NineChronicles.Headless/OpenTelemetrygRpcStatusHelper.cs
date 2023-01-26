using System.Diagnostics;
using Grpc.Core;

namespace NineChronicles.Headless;

public static class OpenTelemetrygRpcStatusHelper
{
    // gRPC StatusCode and OpenTelemetry.CanonicalCode is same.
    public static ActivityStatusCode ConvertStatus(StatusCode code)
    {
        switch (code)
        {
            case StatusCode.OK:
                return ActivityStatusCode.Ok;
            case StatusCode.Cancelled:
                return ActivityStatusCode.Error;
            case StatusCode.Unknown:
                return ActivityStatusCode.Error;
            case StatusCode.InvalidArgument:
                return ActivityStatusCode.Error;
            case StatusCode.DeadlineExceeded:
                return ActivityStatusCode.Error;
            case StatusCode.NotFound:
                return ActivityStatusCode.Error;
            case StatusCode.AlreadyExists:
                return ActivityStatusCode.Error;
            case StatusCode.PermissionDenied:
                return ActivityStatusCode.Error;
            case StatusCode.Unauthenticated:
                return ActivityStatusCode.Error;
            case StatusCode.ResourceExhausted:
                return ActivityStatusCode.Error;
            case StatusCode.FailedPrecondition:
                return ActivityStatusCode.Error;
            case StatusCode.Aborted:
                return ActivityStatusCode.Error;
            case StatusCode.OutOfRange:
                return ActivityStatusCode.Error;
            case StatusCode.Unimplemented:
                return ActivityStatusCode.Error;
            case StatusCode.Internal:
                return ActivityStatusCode.Error;
            case StatusCode.Unavailable:
                return ActivityStatusCode.Error;
            case StatusCode.DataLoss:
                return ActivityStatusCode.Error;
            default:
                // custom status code? use Unknown.
                return ActivityStatusCode.Error;
        }
    }
}
