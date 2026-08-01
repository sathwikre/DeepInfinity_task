# WPF + Local Azure Functions Login Sample

This sample is a local-only login demo. `LoginClient` is a WPF desktop application and `LoginFunction` is a .NET isolated Azure Function. No Azure account or deployment is needed.

## Project layout

```
LoginSample/
├── LoginSample.slnx
├── LoginClient/                 # WPF desktop application
│   ├── Models/
│   │   ├── LoginRequest.cs
│   │   └── LoginResponse.cs
│   ├── Services/ApiService.cs
│   ├── MainWindow.xaml
│   └── MainWindow.xaml.cs
└── LoginFunction/               # HTTP-triggered Azure Function
    ├── Functions/LoginFunction.cs
    ├── Models/
    │   ├── LoginRequest.cs
    │   └── LoginResponse.cs
    ├── Program.cs
    ├── host.json
    └── local.settings.json
```

## Prerequisites

- Windows and Visual Studio 2022 (with **.NET desktop development**; Azure development is optional but useful).
- .NET 8 SDK. This sample targets `net8.0` / `net8.0-windows`.
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) to run the Function with the `func` command.
- Azurite is **not required for this HTTP-only Function**. The standard `AzureWebJobsStorage` setting is nevertheless included for future non-HTTP triggers. If the host requests storage, install/start Azurite or change that setting to a real connection string.

## Create the Function project from the command line

These are the equivalent commands used to create an isolated worker HTTP Function. The complete project is already included here.

```powershell
mkdir LoginFunction
cd LoginFunction
func init --worker-runtime dotnet-isolated --target-framework net8.0
func new --template "HTTP trigger" --name LoginFunction --authlevel anonymous
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http
```

## Run locally

1. Open `LoginSample.slnx` in Visual Studio 2022, or use two PowerShell windows.
2. Restore and build:

   ```powershell
   dotnet restore .\LoginSample.slnx
   dotnet build .\LoginSample.slnx
   ```

3. In the first terminal, start the Function:

   ```powershell
   cd .\LoginFunction
   func start
   ```

   Core Tools reports `http://localhost:7071/api/login` once it has started.

4. In a second terminal, start the WPF client:

   ```powershell
   cd .\LoginClient
   dotnet run
   ```

5. Enter `admin` / `1234` and select **Login**. Any other non-empty credentials return **Invalid Credentials**.

You can also run the client and Function using Visual Studio profiles. Start `LoginFunction` first, then start `LoginClient`.

## Endpoint contract

`POST http://localhost:7071/api/login`

```json
{
  "username": "admin",
  "password": "1234"
}
```

Successful login returns:

```json
{
  "success": true,
  "message": "Login Successful"
}
```

Incorrect credentials return HTTP 200 with `success: false` and `Invalid Credentials`; malformed JSON or missing fields return HTTP 400. Unexpected server errors return HTTP 500.

Try the endpoint independently after starting the Function:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:7071/api/login `
  -ContentType 'application/json' `
  -Body '{"username":"admin","password":"1234"}'
```

## How the applications communicate

When the button is clicked, `MainWindow.xaml.cs` validates the two input fields and disables the button. It calls `ApiService.LoginAsync`, which uses one reusable `HttpClient` to serialize `LoginRequest` as JSON and POST it to `api/login`. The Function binds the HTTP request, deserializes it into its own `LoginRequest` model, validates the values, and serializes a `LoginResponse`. `ApiService` deserializes that JSON response, and the window displays its `Message` in a `MessageBox`. The button is restored in `finally`, including after network or server failures.

## Important files

- `LoginClient/MainWindow.xaml`: defines the username field, password field, and button.
- `LoginClient/MainWindow.xaml.cs`: UI validation, asynchronous click handler, error handling, and message display.
- `LoginClient/Services/ApiService.cs`: contains the HTTP-specific code and response deserialization, keeping it out of the window.
- `LoginClient/Models/*`: typed API request and response contracts for the client.
- `LoginFunction/Program.cs`: starts the .NET isolated Functions worker and configures HTTP support.
- `LoginFunction/Functions/LoginFunction.cs`: implements `POST /api/login`, validation, typed JSON responses, and logging/error handling.
- `LoginFunction/Models/*`: typed Function request and response contracts; no dynamic objects are used.
- `LoginFunction/local.settings.json`: local-only host configuration. Do not commit real connection strings; this project ignores this file and includes a safe sample copy.
- `LoginFunction/host.json`: Function host-level settings.

## NuGet packages

`LoginClient` uses only the .NET 8 built-in WPF and `System.Net.Http.Json` APIs, so it needs no third-party packages.

`LoginFunction` references:

- `Microsoft.Azure.Functions.Worker` 2.1.0
- `Microsoft.Azure.Functions.Worker.Extensions.Http` 3.3.0
- `Microsoft.Azure.Functions.Worker.Sdk` 2.0.5 (analyzer/build SDK)

## Note for real applications

Hardcoded credentials and plain-text passwords are intentional only for this learning example. Production systems should use secure identity/authentication, TLS, secrets management, password hashing, authorization, and a persistent user store.
