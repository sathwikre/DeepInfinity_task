using Microsoft.Extensions.Hosting;

// The standard isolated-worker host is sufficient for this HttpRequestData-based Function.
// ConfigureFunctionsWorkerDefaults sets up Function bindings, logging, and worker services.
Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWorkerDefaults()
    .Build()
    .Run();
