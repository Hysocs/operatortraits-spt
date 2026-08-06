using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace OperatorTraits.Server;

[Injectable]
public sealed class OperatorTraitsRouter(
    JsonUtil jsonUtil,
    OperatorTraitsCallbacks callbacks)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<ResetPaymentRequest>(
            "/operator-traits/reset",
            async (url, request, sessionId, output, cancellationToken) =>
                await callbacks.ResetTraits(request, sessionId)),
        new RouteAction<SaveTraitsRequest>(
            "/operator-traits/save",
            async (url, request, sessionId, output, cancellationToken) =>
                await callbacks.SaveTraits(request, sessionId)),
        new RouteAction<EmptyTraitsRequest>(
            "/operator-traits/load",
            async (url, request, sessionId, output, cancellationToken) =>
                await callbacks.LoadTraits(sessionId)),
        new RouteAction<EmptyTraitsRequest>(
            "/operator-traits/street-tax/simulate",
            async (url, request, sessionId, output, cancellationToken) =>
                await callbacks.SimulateStreetTax(sessionId))
    ]);
